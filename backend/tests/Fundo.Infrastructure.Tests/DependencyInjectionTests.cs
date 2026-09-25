using Fundo.Infrastructure.ExternalServices;
using Fundo.Infrastructure.Outbox;
using Fundo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fundo.Infrastructure.Tests;

public class DependencyInjectionTests
{
    private static readonly Uri ExternalServiceAddress = new("http://localhost:5180");

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<FundoDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        services.AddOutboxDelivery(ExternalServiceAddress);

        // Fails fast on missing dependencies and on scoped services captured by singletons.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    [Fact]
    public void AddOutboxDelivery_RegistersProcessorInScope()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OutboxProcessor>());
        Assert.IsType<ExternalApplicationClient>(scope.ServiceProvider.GetRequiredService<IExternalApplicationClient>());
    }

    [Fact]
    public void AddOutboxDelivery_RegistersBackgroundWorker()
    {
        using var provider = BuildProvider();

        Assert.Contains(provider.GetServices<IHostedService>(), service => service is OutboxBackgroundService);
    }

    [Fact]
    public void AddOutboxDelivery_ConfiguresHttpClientAddressAndTimeout()
    {
        using var provider = BuildProvider();

        var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IExternalApplicationClient));

        Assert.Equal(ExternalServiceAddress, httpClient.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(10), httpClient.Timeout);
    }

    [Fact]
    public void AddOutboxDelivery_WithNullAddress_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddOutboxDelivery(null!));
    }
}
