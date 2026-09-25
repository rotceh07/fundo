using Fundo.Domain.Customers;

namespace Fundo.Application.Abstractions;

public interface ICustomerRepository
{
    Task<Customer?> FindBySsnAsync(Ssn ssn, CancellationToken cancellationToken);

    void Add(Customer customer);

    void Update(Customer customer);
}
