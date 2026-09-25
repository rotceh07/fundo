using Fundo.Application.Abstractions;
using Fundo.Application.Applications.Events;
using Fundo.Infrastructure.Persistence;

namespace Fundo.Infrastructure.Outbox;

// Stages the event as an outbox row in the same DbContext used by the repositories,
// so the business data and the event are committed by the same SaveChanges.
public sealed class OutboxApplicationEventPublisher : IApplicationEventPublisher
{
    private readonly FundoDbContext _dbContext;

    public OutboxApplicationEventPublisher(FundoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public void Publish(ApplicationApprovedEvent applicationEvent)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);

        var payload = OutboxJsonSerializer.Serialize(applicationEvent);

        _dbContext.OutboxMessages.Add(OutboxMessage.Create(applicationEvent, payload));
    }
}
