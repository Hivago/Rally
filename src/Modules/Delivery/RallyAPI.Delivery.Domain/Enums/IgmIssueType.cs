namespace RallyAPI.Delivery.Domain.Enums;

/// <summary>
/// Issue types from the ProRouting SOP. Drives the refund-eligibility policy.
/// </summary>
public enum IgmIssueType
{
    DelayInDelivery = 1,
    FakePickup = 2,
    FoodSpillage = 3,
    RudeAgent = 4,
    RiderRunaway = 5,

    /// <summary>
    /// Marked delivered in our system but customer never received it.
    /// Per SOP: NOT eligible for refund.
    /// </summary>
    DeliveredButNotMarked = 6
}
