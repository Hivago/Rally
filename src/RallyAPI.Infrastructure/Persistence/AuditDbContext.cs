using Microsoft.EntityFrameworkCore;
using RallyAPI.SharedKernel.Domain.Entities;

namespace RallyAPI.Infrastructure.Persistence;

public class AuditDbContext : DbContext
{
    public DbSet<WebhookAuditLog> WebhookAuditLogs { get; set; } = null!;

    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("audit");

        modelBuilder.Entity<WebhookAuditLog>(builder =>
        {
            builder.ToTable("webhook_audit_logs");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Source).HasMaxLength(50);
            builder.Property(x => x.EventId).HasMaxLength(200);
            builder.Property(x => x.ProcessingStatus).HasMaxLength(50);
            // IP as string is easy
            builder.Property(x => x.SourceIp).HasMaxLength(45);
        });
    }
}
