using Fundo.Domain.Applications;
using Fundo.Domain.Customers;
using Fundo.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Persistence;

public sealed class FundoDbContext : DbContext
{
    public FundoDbContext(DbContextOptions<FundoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FundoDbContext).Assembly);
    }
}
