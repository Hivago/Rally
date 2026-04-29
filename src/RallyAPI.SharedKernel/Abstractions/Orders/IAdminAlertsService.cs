namespace RallyAPI.SharedKernel.Abstractions.Orders;

/// <summary>
/// Returns active operational alerts for the admin dashboard.
/// Implemented in Orders.Infrastructure, consumed by Users.Application.
///
/// Alert types currently emitted:
///   - "escalated"          — order escalated to admin (IsEscalated flag, still in Paid)
///   - "stuck_payment"      — order has been Pending payment past the hard-cancel threshold
///
/// Not yet emitted (deferred — needs Delivery-module signals):
///   - "dispatch_failed"    — delivery offer rejected by all candidate riders
///   - "stuck_in_dispatch"  — order ReadyForPickup with no rider assigned
/// </summary>
public interface IAdminAlertsService
{
    Task<IReadOnlyList<AdminAlert>> GetActiveAlertsAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record AdminAlert(
    string Type,
    Guid OrderId,
    string OrderNumber,
    string Message,
    string Severity,
    DateTime RaisedAt);
