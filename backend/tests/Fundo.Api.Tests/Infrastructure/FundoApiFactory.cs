using Fundo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Fundo.Api.Tests.Infrastructure;

// Runs the real API against its own temporary SQLite file. The startup migration creates the schema.
// Only the background worker is removed, so it cannot clear outbox payloads while a test asserts on them.
public sealed class FundoApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"fundo-api-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Since EF Core 9 the options configuration is registered separately from the options,
            // so both are removed to make sure the runtime connection string is not applied.
            services.RemoveAll<DbContextOptions<FundoDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<FundoDbContext>>();
            services.RemoveAll<FundoDbContext>();
            services.AddDbContext<FundoDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));

            services.RemoveAll<IHostedService>();
        });
    }

    public async Task<T> QueryAsync<T>(Func<FundoDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FundoDbContext>();

        return await query(dbContext);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        // Pooled connections keep the file open, which would block deletion on some platforms.
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            File.Delete(_databasePath + suffix);
        }
    }
}
