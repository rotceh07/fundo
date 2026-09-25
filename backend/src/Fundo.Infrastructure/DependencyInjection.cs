using Fundo.Infrastructure.ExternalServices;
using Fundo.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Fundo.Infrastructure;

public static class DependencyInjection
{
    // The default HttpClient timeout is 100 seconds, far too long for a sequential worker
    // that polls every 2 seconds.
    private static readonly TimeSpan ExternalRequestTimeout = TimeSpan.FromSeconds(10);

    // Registers background delivery of outbox messages. Expects FundoDbContext to be registered by the host.
    public static IServiceCollection AddOutboxDelivery(this IServiceCollection services, Uri externalServiceBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(externalServiceBaseAddress);

        services.AddHttpClient<IExternalApplicationClient, ExternalApplicationClient>(client =>
        {
            client.BaseAddress = externalServiceBaseAddress;
            client.Timeout = ExternalRequestTimeout;
        });

        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();

        return services;
    }
}
