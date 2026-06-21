using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Orders.Infrastructure.Outbox;

namespace RallyAPI.Orders.Infrastructure.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        // Schema is "orders" via OrdersDbContext.HasDefaultSchema.
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Content).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OccurredOn).IsRequired();
        builder.Property(x => x.RetryCount).IsRequired();

        // Partial index so the processor scans only the unprocessed backlog.
        builder.HasIndex(x => x.OccurredOn)
            .HasFilter("\"ProcessedOn\" IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Unprocessed");
    }
}
