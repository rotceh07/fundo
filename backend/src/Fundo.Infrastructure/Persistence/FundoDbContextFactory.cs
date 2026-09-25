using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fundo.Infrastructure.Persistence;

// Design-time only. Lets the EF Core tools build the context without the API project.
public sealed class FundoDbContextFactory : IDesignTimeDbContextFactory<FundoDbContext>
{
    public FundoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FundoDbContext>()
            .UseSqlite("Data Source=fundo-design.db")
            .Options;

        return new FundoDbContext(options);
    }
}
