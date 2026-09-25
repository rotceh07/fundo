using Fundo.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fundo.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.FirstName).IsRequired();
        builder.Property(x => x.LastName).IsRequired();
        builder.Property(x => x.Address).IsRequired();
        builder.Property(x => x.State).IsRequired();
        builder.Property(x => x.CompanyName).IsRequired();

        builder.Property(x => x.Ssn)
            .HasConversion(ssn => ssn.Value, value => Ssn.Create(value))
            .IsRequired();

        // Enforces one customer per SSN even when two requests race past the lookup.
        builder.HasIndex(x => x.Ssn)
            .IsUnique()
            .HasDatabaseName("UX_Customers_Ssn");
    }
}
