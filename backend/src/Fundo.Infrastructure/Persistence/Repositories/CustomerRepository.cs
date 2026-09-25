using Fundo.Application.Abstractions;
using Fundo.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly FundoDbContext _dbContext;

    public CustomerRepository(FundoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    // Tracked on purpose: a returning customer is modified in the same unit of work.
    public Task<Customer?> FindBySsnAsync(Ssn ssn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ssn);

        return _dbContext.Customers.SingleOrDefaultAsync(x => x.Ssn == ssn, cancellationToken);
    }

    public void Add(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        _dbContext.Customers.Add(customer);
    }

    public void Update(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        _dbContext.Customers.Update(customer);
    }
}
