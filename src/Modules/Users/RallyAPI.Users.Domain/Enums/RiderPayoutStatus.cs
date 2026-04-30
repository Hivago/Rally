namespace RallyAPI.Users.Domain.Enums;

/// <summary>
/// Lifecycle of a rider payout cycle. Mirrors the restaurant-side PayoutStatus
/// in Orders.Domain but stays in this module since the rider lives here.
/// </summary>
public enum RiderPayoutStatus
{
    Pending = 0,
    Processing = 1,
    Paid = 2,
    Failed = 3,
    OnHold = 4
}
