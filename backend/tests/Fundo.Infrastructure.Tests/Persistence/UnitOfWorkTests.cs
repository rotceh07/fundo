using Fundo.Domain.Applications;
using Fundo.Domain.Customers;
using Fundo.Infrastructure.Persistence;
using Fundo.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Tests.Persistence;

public class UnitOfWorkTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static Customer NewCustomer(string ssn) =>
        Customer.Create("John", "Doe", "100 Main St", "FL", "Acme", Ssn.Create(ssn));

    [Fact]
    public async Task SaveChangesAsync_WithCustomerAndApplication_CommitsBothTogether()
    {
        var customer = NewCustomer("123-45-6789");
        var application = LoanApplication.Create(customer.Id, 10_000m);

        await using (var context = _database.CreateContext())
        {
            new CustomerRepository(context).Add(customer);
            new LoanApplicationRepository(context).Add(application);
            await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
        }

        await using var verification = _database.CreateContext();
        Assert.True(await verification.Customers.AnyAsync(x => x.Id == customer.Id));
        Assert.True(await verification.LoanApplications.AnyAsync(x => x.Id == application.Id));
    }

    [Fact]
    public async Task SaveChangesAsync_WhenLaterCommandFails_RollsBackEarlierCommands()
    {
        var existing = NewCustomer("111-11-1111");
        await using (var context = _database.CreateContext())
        {
            context.AddRange(existing, LoanApplication.Create(existing.Id, 10_000m));
            await context.SaveChangesAsync();
        }

        var newCustomer = NewCustomer("222-22-2222");
        var changesBefore = await _database.ScalarAsync("SELECT total_changes()");

        // EF writes Customers before LoanApplications, so the valid insert runs first
        // and the duplicate application for the existing customer fails afterwards.
        await using (var context = _database.CreateContext())
        {
            new CustomerRepository(context).Add(newCustomer);
            new LoanApplicationRepository(context).Add(LoanApplication.Create(existing.Id, 20_000m));

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                new UnitOfWork(context).SaveChangesAsync(CancellationToken.None));
        }

        var changesAfter = await _database.ScalarAsync("SELECT total_changes()");
        Assert.True(changesAfter > changesBefore, "The valid insert should have executed before the failure.");

        await using var verification = _database.CreateContext();
        Assert.False(await verification.Customers.AnyAsync(x => x.Id == newCustomer.Id));
        Assert.Equal(1, await verification.Customers.CountAsync());
        Assert.Equal(1, await verification.LoanApplications.CountAsync());
    }
}
