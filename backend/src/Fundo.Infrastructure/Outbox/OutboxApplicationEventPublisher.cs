using System.Text.Json;
using System.Text.Json.Serialization;
using Fundo.Application.Abstractions;
using Fundo.Application.Applications.Events;
using Fundo.Infrastructure.Persistence;

namespace Fundo.Infrastructure.Outbox;

// Stages the event as an outbox row in the same DbContext used by the repositories,
// so the business data and the event are committed by the same SaveChanges.
public sealed class OutboxApplicationEventPublisher : IApplicationEventPublisher
{
    // Operation is written as text so reordering the enum never changes the meaning of pending messages.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter<ApplicationEventOperation>() }
    };

    private readonly FundoDbContext _dbContext;

    public OutboxApplicationEventPublisher(FundoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public void Publish(ApplicationApprovedEvent applicationEvent)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);

        var payload = JsonSerializer.Serialize(applicationEvent, SerializerOptions);

        _dbContext.OutboxMessages.Add(OutboxMessage.Create(applicationEvent, payload));
    }
}
