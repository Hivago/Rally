namespace RallyAPI.Delivery.Domain.Enums;

/// <summary>
/// Order category required by 3PL providers (e.g. ProRouting) to route to
/// the correct LSP and apply category-specific RTO/disposal rules.
/// </summary>
public enum OrderCategory
{
    /// <summary>
    /// Food & beverage. Maps to ProRouting "F&B".
    /// Eligible for RTO Disposed when delivery fails.
    /// </summary>
    FoodAndBeverage = 1,

    /// <summary>
    /// Grocery. Maps to ProRouting "Grocery".
    /// Must be returned to store on RTO (no disposal).
    /// </summary>
    Grocery = 2,

    /// <summary>
    /// Pharma. Maps to ProRouting "Pharma".
    /// Must be returned to store on RTO (no disposal).
    /// </summary>
    Pharma = 3
}
