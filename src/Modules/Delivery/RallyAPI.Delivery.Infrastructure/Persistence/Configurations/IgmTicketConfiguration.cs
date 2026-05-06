using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Delivery.Domain.Entities;

namespace RallyAPI.Delivery.Infrastructure.Persistence.Configurations;

public sealed class IgmTicketConfiguration : IEntityTypeConfiguration<IgmTicket>
{
    public void Configure(EntityTypeBuilder<IgmTicket> builder)
    {
        builder.ToTable("igm_tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.DeliveryRequestId)
            .HasColumnName("delivery_request_id")
            .IsRequired();

        builder.Property(t => t.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(t => t.OrderNumber)
            .HasColumnName("order_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.IssueType)
            .HasColumnName("issue_type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.Category)
            .HasColumnName("category")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.SubCategory)
            .HasColumnName("sub_category")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.DescriptionShort)
            .HasColumnName("description_short")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.DescriptionLong)
            .HasColumnName("description_long")
            .HasMaxLength(2000);

        builder.Property(t => t.State)
            .HasColumnName("state")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.ExternalIssueId)
            .HasColumnName("external_issue_id")
            .HasMaxLength(100);

        builder.Property(t => t.ResolutionAction)
            .HasColumnName("resolution_action")
            .HasConversion<int>();

        builder.Property(t => t.ResolutionShortDesc)
            .HasColumnName("resolution_short_desc")
            .HasMaxLength(500);

        builder.Property(t => t.ResolutionLongDesc)
            .HasColumnName("resolution_long_desc")
            .HasMaxLength(2000);

        builder.Property(t => t.RefundAmount)
            .HasColumnName("refund_amount")
            .HasPrecision(18, 2);

        builder.Property(t => t.Rating)
            .HasColumnName("rating")
            .HasMaxLength(20);

        builder.Property(t => t.RefundByLsp)
            .HasColumnName("refund_by_lsp");

        builder.Property(t => t.RefundToClient)
            .HasColumnName("refund_to_client");

        builder.Property(t => t.RaisedByAdminId)
            .HasColumnName("raised_by_admin_id")
            .IsRequired();

        builder.Property(t => t.PushedAt)
            .HasColumnName("pushed_at");

        builder.Property(t => t.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(t => t.ClosedAt)
            .HasColumnName("closed_at");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(t => t.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();

        builder.Ignore(t => t.DomainEvents);

        builder.HasQueryFilter(t => !t.DeletedAt.HasValue);

        builder.HasIndex(t => t.DeliveryRequestId)
            .HasDatabaseName("ix_igm_tickets_delivery_request_id");

        builder.HasIndex(t => t.OrderId)
            .HasDatabaseName("ix_igm_tickets_order_id");

        builder.HasIndex(t => t.ExternalIssueId)
            .HasDatabaseName("ix_igm_tickets_external_issue_id");

        builder.HasIndex(t => t.State)
            .HasDatabaseName("ix_igm_tickets_state");
    }
}
