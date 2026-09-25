using Fundo.Domain.Applications;
using Fundo.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Tests.Persistence;

public class PersistenceConstraintTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static Customer NewCustomer(string ssn) =>
        Customer.Create("John", "Doe", "100 Main St", "FL", "Acme", Ssn.Create(ssn));

    private async Task SaveAsync(params object[] entities)
    {
        await using var context = _database.CreateContext();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task SaveChanges_WithDuplicateSsn_ThrowsDbUpdateException()
    {
        await SaveAsync(NewCustomer("123-45-6789"));

        await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(NewCustomer("123456789")));
    }

    [Fact]
    public async Task SaveChanges_WithSecondApplicationForSameCustomer_ThrowsDbUpdateException()
    {
        var customer = NewCustomer("123-45-6789");
        await SaveAsync(customer, LoanApplication.Create(customer.Id, 10_000m));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            SaveAsync(LoanApplication.Create(customer.Id, 20_000m)));
    }

    [Fact]
    public async Task SaveChanges_WithApplicationForMissingCustomer_ThrowsDbUpdateException()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            SaveAsync(LoanApplication.Create(Guid.NewGuid(), 10_000m)));
    }
}
