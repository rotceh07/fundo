using Fundo.Application.Applications.Events;

namespace Fundo.Infrastructure.ExternalServices;

public interface IExternalApplicationClient
{
    Task SendAsync(ApplicationApprovedEvent applicationEvent, CancellationToken cancellationToken);
}
