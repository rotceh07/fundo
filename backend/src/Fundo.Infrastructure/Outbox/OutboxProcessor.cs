using System.Text.Json;
using Fundo.Application.Applications.Events;
using Fundo.Infrastructure.ExternalServices;
using Fundo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Outbox;

// Delivers pending outbox messages in Id order with at least once semantics.
// A failed message blocks the later messages of the same customer for the rest of the pass,
// while other customers keep being processed. Failed messages are retried on the next pass.
public sealed class OutboxProcessor
{
    private readonly FundoDbContext _dbContext;
    private readonly IExternalApplicationClient _externalClient;

    public OutboxProcessor(FundoDbContext dbContext, IExternalApplicationClient externalClient)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(externalClient);

        _dbContext = dbContext;
        _externalClient = externalClient;
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var messages = await _dbContext.OutboxMessages
            .Where(x => x.ProcessedAtUtc == null)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var blockedCustomers = new HashSet<Guid>();

        foreach (var message in messages)
        {
            if (blockedCustomers.Contains(message.CustomerId))
            {
                continue;
            }

            var error = await TryDeliverAsync(message, cancellationToken);

            if (error is null)
            {
                message.MarkProcessed(DateTimeOffset.UtcNow);
            }
            else
            {
                message.RegisterFailure(error);
                blockedCustomers.Add(message.CustomerId);
            }

            // Saved per message to keep the window for duplicate deliveries small.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    // Returns null when the message was delivered, otherwise a generic error that is safe to store.
    // Unexpected exceptions are not caught so a bug surfaces instead of turning into endless retries.
    private async Task<string?> TryDeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.Payload))
        {
            return "Pending outbox message has no payload.";
        }

        ApplicationApprovedEvent applicationEvent;

        try
        {
            applicationEvent = OutboxJsonSerializer.Deserialize(message.Payload);
        }
        catch (JsonException)
        {
            return "Invalid outbox payload.";
        }

        if (applicationEvent.CustomerId != message.CustomerId)
        {
            return "Outbox payload customer does not match message metadata.";
        }

        try
        {
            await _externalClient.SendAsync(applicationEvent, cancellationToken);
            return null;
        }
        catch (HttpRequestException exception)
        {
            return exception.StatusCode is { } statusCode
                ? $"External service returned HTTP {(int)statusCode}."
                : "External service request failed.";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation that did not come from our token.
            return "External service request timed out.";
        }
    }
}
