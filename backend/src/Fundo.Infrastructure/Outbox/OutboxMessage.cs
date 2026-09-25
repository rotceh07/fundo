using Fundo.Application.Applications.Events;

namespace Fundo.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    private const int MaxErrorLength = 500;

    // Required for persistence materialization.
    private OutboxMessage()
    {
    }

    // Autoincremented by the database and used as the delivery sequence.
    public long Id { get; private set; }

    // Stored outside the payload so messages can be ordered per customer without reading PII.
    public Guid CustomerId { get; private set; }

    public string EventType { get; private set; } = null!;

    public string Operation { get; private set; } = null!;

    // Contains the full event, SSN included, while pending. Nullable so it can be
    // cleared after a successful delivery while the metadata is kept.
    public string? Payload { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public int RetryCount { get; private set; }

    public string? LastError { get; private set; }

    internal static OutboxMessage Create(ApplicationApprovedEvent applicationEvent, string payload)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        if (applicationEvent.CustomerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(applicationEvent));
        }

        return new OutboxMessage
        {
            CustomerId = applicationEvent.CustomerId,
            EventType = nameof(ApplicationApprovedEvent),
            Operation = applicationEvent.Operation.ToString(),
            Payload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            RetryCount = 0
        };
    }

    // Clears the payload so personal data is not retained after a successful delivery.
    // RetryCount is kept as history of the previous failures.
    public void MarkProcessed(DateTimeOffset processedAtUtc)
    {
        if (processedAtUtc == default)
        {
            throw new ArgumentException("Processed time is required.", nameof(processedAtUtc));
        }

        ProcessedAtUtc = processedAtUtc;
        Payload = null;
        LastError = null;
    }

    // Callers must pass a controlled generic message, never exception details or response bodies.
    public void RegisterFailure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        var trimmed = error.Trim();

        RetryCount++;
        LastError = trimmed.Length > MaxErrorLength ? trimmed[..MaxErrorLength] : trimmed;
    }
}
