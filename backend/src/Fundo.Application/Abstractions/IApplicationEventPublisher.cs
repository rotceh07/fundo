using Fundo.Application.Applications.Events;

namespace Fundo.Application.Abstractions;

// Publishing stages the event for the current unit of work.
// It must not call the external service directly.
public interface IApplicationEventPublisher
{
    void Publish(ApplicationApprovedEvent applicationEvent);
}
