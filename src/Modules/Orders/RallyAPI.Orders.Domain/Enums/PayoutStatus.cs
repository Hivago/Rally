namespace RallyAPI.Orders.Domain.Enums;

public enum PayoutStatus
{
    Pending = 0,
    Processing = 1,
    Paid = 2,
    Failed = 3,
    /// <summary>
    /// Admin paused this payout — skipped by the auto-run scheduler until released.
    /// </summary>
    OnHold = 4
}
