using Fundo.Application.Abstractions;

namespace Fundo.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly FundoDbContext _dbContext;

    public UnitOfWork(FundoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    // A single SaveChanges runs all staged changes inside one database transaction.
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
