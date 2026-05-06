using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Delivery.Domain.Entities;

/// <summary>
/// Local mirror of a ProRouting issue (IGM ticket). We own the lifecycle locally
/// so admins can raise, view, and close tickets without round-tripping to
/// ProRouting on every read, and so we can track our own SLA timers.
///
/// Phase 1: entity + repository only. Auto-trigger rules (e.g. delay watcher
/// raising IGM automatically) are deferred to Phase 2.
/// </summary>
public sealed class IgmTicket : AggregateRoot
{
    private IgmTicket() { }

    public Guid DeliveryRequestId { get; private set; }
    public Guid OrderId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;

    public IgmIssueType IssueType { get; private set; }

    /// <summary>
    /// ProRouting issue category (e.g. "FULFILLMENT").
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// ProRouting sub-category (e.g. "FLM02", "FLM08").
    /// </summary>
    public string SubCategory { get; private set; } = string.Empty;

    public string DescriptionShort { get; private set; } = string.Empty;
    public string? DescriptionLong { get; private set; }

    public IgmTicketState State { get; private set; }

    /// <summary>
    /// ProRouting issue id returned from /issue (e.g. "issmp2pre_xxx_yyy").
    /// Null until the ticket is pushed.
    /// </summary>
    public string? ExternalIssueId { get; private set; }

    // Resolution (populated when /issue_status returns Resolved)
    public IgmResolutionAction? ResolutionAction { get; private set; }
    public string? ResolutionShortDesc { get; private set; }
    public string? ResolutionLongDesc { get; private set; }
    public decimal? RefundAmount { get; private set; }

    // Close (populated when /issue_close is called)
    public string? Rating { get; private set; }       // "THUMBS-UP" / "THUMBS-DOWN"
    public bool? RefundByLsp { get; private set; }
    public bool? RefundToClient { get; private set; }

    // Audit
    public Guid RaisedByAdminId { get; private set; }
    public DateTime? PushedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    public static IgmTicket Create(
        Guid id,
        Guid deliveryRequestId,
        Guid orderId,
        string orderNumber,
        IgmIssueType issueType,
        string category,
        string subCategory,
        string descriptionShort,
        string? descriptionLong,
        Guid raisedByAdminId)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category is required", nameof(category));
        if (string.IsNullOrWhiteSpace(subCategory))
            throw new ArgumentException("SubCategory is required", nameof(subCategory));
        if (string.IsNullOrWhiteSpace(descriptionShort))
            throw new ArgumentException("DescriptionShort is required", nameof(descriptionShort));

        return new IgmTicket
        {
            Id = id,
            DeliveryRequestId = deliveryRequestId,
            OrderId = orderId,
            OrderNumber = orderNumber,
            IssueType = issueType,
            Category = category,
            SubCategory = subCategory,
            DescriptionShort = descriptionShort,
            DescriptionLong = descriptionLong,
            State = IgmTicketState.Open,
            RaisedByAdminId = raisedByAdminId
        };
    }

    /// <summary>
    /// Marks the ticket as pushed to ProRouting after a successful /issue call.
    /// </summary>
    public void MarkPushed(string externalIssueId)
    {
        if (State != IgmTicketState.Open)
            throw new InvalidOperationException(
                $"Only Open tickets can be pushed. Current: {State}");
        if (string.IsNullOrWhiteSpace(externalIssueId))
            throw new ArgumentException("externalIssueId is required", nameof(externalIssueId));

        ExternalIssueId = externalIssueId;
        State = IgmTicketState.Processing;
        PushedAt = DateTime.UtcNow;
        MarkAsUpdated();
    }

    /// <summary>
    /// Records the resolution returned by /issue_status.
    /// </summary>
    public void MarkResolved(
        IgmResolutionAction action,
        string? shortDesc,
        string? longDesc,
        decimal? refundAmount)
    {
        if (State != IgmTicketState.Processing)
            throw new InvalidOperationException(
                $"Only Processing tickets can be resolved. Current: {State}");

        ResolutionAction = action;
        ResolutionShortDesc = shortDesc;
        ResolutionLongDesc = longDesc;
        RefundAmount = refundAmount;
        State = IgmTicketState.Resolved;
        ResolvedAt = DateTime.UtcNow;
        MarkAsUpdated();
    }

    /// <summary>
    /// Closes the ticket after /issue_close is called.
    /// </summary>
    public void Close(string rating, bool refundByLsp, bool refundToClient)
    {
        if (State != IgmTicketState.Resolved)
            throw new InvalidOperationException(
                $"Only Resolved tickets can be closed. Current: {State}");

        Rating = rating;
        RefundByLsp = refundByLsp;
        RefundToClient = refundToClient;
        State = IgmTicketState.Closed;
        ClosedAt = DateTime.UtcNow;
        MarkAsUpdated();
    }
}
