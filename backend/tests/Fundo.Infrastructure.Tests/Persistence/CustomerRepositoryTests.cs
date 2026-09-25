using Fundo.Domain.Customers;
using Fundo.Infrastructure.Persistence;
using Fundo.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Tests.Persistence;

public class CustomerRepositoryTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static Customer NewCustomer(string ssn = "123-45-6789") =>
        Customer.Create("John", "Doe", "100 Main St", "FL", "Acme", Ssn.Create(ssn));

    private async Task SaveAsync(Customer customer)
    {
        await using var context = _database.CreateContext();
        new CustomerRepository(context).Add(customer);
        await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Add_ThenSaveChanges_PersistsAllFields()
    {
        var customer = NewCustomer();
        await SaveAsync(customer);

        await using var context = _database.CreateContext();
        var loaded = await new CustomerRepository(context).FindBySsnAsync(customer.Ssn, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(customer.Id, loaded.Id);
        Assert.Equal("John", loaded.FirstName);
        Assert.Equal("Doe", loaded.LastName);
        Assert.Equal("100 Main St", loaded.Address);
        Assert.Equal("FL", loaded.State);
        Assert.Equal("Acme", loaded.CompanyName);
        Assert.Equal(Ssn.Create("123456789"), loaded.Ssn);
    }

    [Fact]
    public async Task FindBySsnAsync_WithDifferentlyFormattedSsn_FindsSameCustomer()
    {
        var customer = NewCustomer("123-45-6789");
        await SaveAsync(customer);

        await using var context = _database.CreateContext();
        var loaded = await new CustomerRepository(context).FindBySsnAsync(Ssn.Create("123456789"), CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(customer.Id, loaded.Id);
    }

    [Fact]
    public async Task FindBySsnAsync_WithUnknownSsn_ReturnsNull()
    {
        await SaveAsync(NewCustomer());

        await using var context = _database.CreateContext();
        var loaded = await new CustomerRepository(context).FindBySsnAsync(Ssn.Create("999-99-9999"), CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task FindBySsnAsync_ReturnsTrackedEntity()
    {
        var customer = NewCustomer();
        await SaveAsync(customer);

        await using var context = _database.CreateContext();
        var loaded = await new CustomerRepository(context).FindBySsnAsync(customer.Ssn, CancellationToken.None);

        Assert.Equal(EntityState.Unchanged, context.Entry(loaded!).State);
    }

    [Fact]
    public async Task Add_WithoutSaveChanges_DoesNotPersistCustomer()
    {
        var customer = NewCustomer();

        await using (var context = _database.CreateContext())
        {
            new CustomerRepository(context).Add(customer);
        }

        await using var verification = _database.CreateContext();
        Assert.False(await verification.Customers.AnyAsync(x => x.Id == customer.Id));
    }

    [Fact]
    public async Task Update_ThenSaveChanges_PersistsNewProfileAndKeepsIdentity()
    {
        var customer = NewCustomer();
        await SaveAsync(customer);

        await using (var context = _database.CreateContext())
        {
            var repository = new CustomerRepository(context);
            var loaded = await repository.FindBySsnAsync(customer.Ssn, CancellationToken.None);

            loaded!.UpdateProfile("Jane", "Smith", "200 Oak Ave", "TX", "Globex");
            repository.Update(loaded);
            await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
        }

        await using var verification = _database.CreateContext();
        var updated = await verification.Customers.SingleAsync();

        Assert.Equal(customer.Id, updated.Id);
        Assert.Equal(customer.Ssn, updated.Ssn);
        Assert.Equal("Jane", updated.FirstName);
        Assert.Equal("Smith", updated.LastName);
        Assert.Equal("200 Oak Ave", updated.Address);
        Assert.Equal("TX", updated.State);
        Assert.Equal("Globex", updated.CompanyName);
    }
}
