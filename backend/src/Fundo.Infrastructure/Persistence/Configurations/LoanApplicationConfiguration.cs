using Fundo.Domain.Applications;
using Fundo.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fundo.Infrastructure.Persistence.Configurations;

internal sealed class LoanApplicationConfiguration : IEntityTypeConfiguration<LoanApplication>
{
    public void Configure(EntityTypeBuilder<LoanApplication> builder)
    {
        builder.ToTable("LoanApplications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // Kept as decimal. SQLite stores it as TEXT, which round-trips exactly;
        // we never sort or filter by amount in SQL, so no conversion is needed.
        builder.Property(x => x.RequestedAmount).IsRequired();

        builder.Property(x => x.CustomerId).IsRequired();

        builder.HasOne<Customer>()
            .WithOne()
            .HasForeignKey<LoanApplication>(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Enforces one application per customer.
        builder.HasIndex(x => x.CustomerId)
            .IsUnique()
            .HasDatabaseName("UX_LoanApplications_CustomerId");
    }
}
