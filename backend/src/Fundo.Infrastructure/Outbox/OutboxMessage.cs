using Fundo.Application.Applications.Events;

namespace Fundo.Infrastructure.Outbox;

public sealed class OutboxMessage
{
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
}
