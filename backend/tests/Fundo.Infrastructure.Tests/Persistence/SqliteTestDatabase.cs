using Fundo.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Tests.Persistence;

// One private in-memory SQLite database per test. The connection stays open for the
// lifetime of the test because the database disappears when it closes.
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly DbContextOptions<FundoDbContext> _options;

    private SqliteTestDatabase(SqliteConnection connection)
    {
        Connection = connection;
        _options = new DbContextOptionsBuilder<FundoDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    public SqliteConnection Connection { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var database = new SqliteTestDatabase(connection);

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();

        return database;
    }

    public FundoDbContext CreateContext() => new(_options);

    public async Task<long> ScalarAsync(string sql)
    {
        await using var command = Connection.CreateCommand();
        command.CommandText = sql;

        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
