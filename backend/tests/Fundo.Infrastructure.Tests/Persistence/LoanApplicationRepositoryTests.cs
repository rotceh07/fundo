using Fundo.Domain.Applications;
using Fundo.Domain.Customers;
using Fundo.Infrastructure.Persistence;
using Fundo.Infrastructure.Persistence.Repositories;

namespace Fundo.Infrastructure.Tests.Persistence;

public class LoanApplicationRepositoryTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private async Task<LoanApplication> SaveWithCustomerAsync(decimal amount)
    {
        var customer = Customer.Create("John", "Doe", "100 Main St", "FL", "Acme", Ssn.Create("123-45-6789"));
        var application = LoanApplication.Create(customer.Id, amount);

        await using var context = _database.CreateContext();
        new CustomerRepository(context).Add(customer);
        new LoanApplicationRepository(context).Add(application);
        await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        return application;
    }

    private async Task<LoanApplication?> FindAsync(Guid customerId)
    {
        await using var context = _database.CreateContext();
        return await new LoanApplicationRepository(context).FindByCustomerIdAsync(customerId, CancellationToken.None);
    }

    [Fact]
    public async Task FindByCustomerIdAsync_AfterSave_ReturnsPersistedApplication()
    {
        var application = await SaveWithCustomerAsync(10_000m);

        var loaded = await FindAsync(application.CustomerId);

        Assert.NotNull(loaded);
        Assert.Equal(application.Id, loaded.Id);
        Assert.Equal(application.CustomerId, loaded.CustomerId);
        Assert.Equal(10_000m, loaded.RequestedAmount);
    }

    [Fact]
    public async Task FindByCustomerIdAsync_WithUnknownCustomer_ReturnsNull()
    {
        await SaveWithCustomerAsync(10_000m);

        Assert.Null(await FindAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RequestedAmount_WithCents_RoundTripsExactly()
    {
        var application = await SaveWithCustomerAsync(12345.67m);

        var loaded = await FindAsync(application.CustomerId);

        Assert.Equal(12345.67m, loaded!.RequestedAmount);
    }

    [Fact]
    public async Task Update_ThenSaveChanges_PersistsExactAmountAndKeepsIdentity()
    {
        var application = await SaveWithCustomerAsync(10_000m);

        await using (var context = _database.CreateContext())
        {
            var repository = new LoanApplicationRepository(context);
            var loaded = await repository.FindByCustomerIdAsync(application.CustomerId, CancellationToken.None);

            loaded!.UpdateRequestedAmount(25000.75m);
            repository.Update(loaded);
            await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
        }

        var updated = await FindAsync(application.CustomerId);

        Assert.Equal(application.Id, updated!.Id);
        Assert.Equal(application.CustomerId, updated.CustomerId);
        Assert.Equal(25000.75m, updated.RequestedAmount);
    }
}
