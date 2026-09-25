using Fundo.Application.Abstractions;
using Fundo.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Persistence.Repositories;

public sealed class LoanApplicationRepository : ILoanApplicationRepository
{
    private readonly FundoDbContext _dbContext;

    public LoanApplicationRepository(FundoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public Task<LoanApplication?> FindByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return _dbContext.LoanApplications.SingleOrDefaultAsync(x => x.CustomerId == customerId, cancellationToken);
    }

    public void Add(LoanApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        _dbContext.LoanApplications.Add(application);
    }

    public void Update(LoanApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        _dbContext.LoanApplications.Update(application);
    }
}
