using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fundo.Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        // No foreign key to Customers on purpose: the outbox is integration history
        // and must not follow the customer lifecycle.
        builder.Property(x => x.CustomerId).IsRequired();
        builder.Property(x => x.EventType).IsRequired();
        builder.Property(x => x.Operation).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.RetryCount).IsRequired();

        builder.HasIndex(x => new { x.ProcessedAtUtc, x.Id })
            .HasDatabaseName("IX_OutboxMessages_ProcessedAtUtc_Id");

        builder.HasIndex(x => new { x.CustomerId, x.Id })
            .HasDatabaseName("IX_OutboxMessages_CustomerId_Id");
    }
}
