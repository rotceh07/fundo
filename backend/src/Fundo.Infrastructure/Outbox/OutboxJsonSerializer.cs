using System.Text.Json;
using System.Text.Json.Serialization;
using Fundo.Application.Applications.Events;

namespace Fundo.Infrastructure.Outbox;

// Shared by the publisher that writes the payload and the processor that reads it back.
internal static class OutboxJsonSerializer
{
    // Operation is written as text so reordering the enum never changes the meaning of pending messages.
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter<ApplicationEventOperation>() }
    };

    public static string Serialize(ApplicationApprovedEvent applicationEvent)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);

        return JsonSerializer.Serialize(applicationEvent, Options);
    }

    public static ApplicationApprovedEvent Deserialize(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return JsonSerializer.Deserialize<ApplicationApprovedEvent>(payload, Options)
            ?? throw new JsonException("Outbox payload is empty.");
    }
}
