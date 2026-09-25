using Fundo.Domain.Applications;

namespace Fundo.Application.Abstractions;

public interface ILoanApplicationRepository
{
    Task<LoanApplication?> FindByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken);

    void Add(LoanApplication application);

    void Update(LoanApplication application);
}
