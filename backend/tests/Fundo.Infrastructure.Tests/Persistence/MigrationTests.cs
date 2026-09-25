using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Tests.Persistence;

public class MigrationTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task MigrateAsync_OnEmptyDatabase_AppliesInitialCreate()
    {
        await using var context = _database.CreateContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();
        var pending = await context.Database.GetPendingMigrationsAsync();

        Assert.Contains(applied, migration => migration.EndsWith("_InitialCreate"));
        Assert.Empty(pending);
    }

    [Theory]
    [InlineData("table", "Customers")]
    [InlineData("table", "LoanApplications")]
    [InlineData("index", "UX_Customers_Ssn")]
    [InlineData("index", "UX_LoanApplications_CustomerId")]
    public async Task MigrateAsync_OnEmptyDatabase_CreatesSchemaObject(string type, string name)
    {
        var count = await _database.ScalarAsync(
            $"SELECT COUNT(*) FROM sqlite_master WHERE type = '{type}' AND name = '{name}'");

        Assert.Equal(1, count);
    }

    [Fact]
    public void Model_HasNoChangesPendingAgainstLatestMigration()
    {
        using var context = _database.CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
