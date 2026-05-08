using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Domain.Abstractions;

/// <summary>
/// Service interface for validating order-related data.
/// Abstracted for flexibility in validation rules.
/// </summary>
public interface IOrderValidationService
{
    /// <summary>
    /// Validates that customer exists and is active.
    /// </summary>
    Task<Result> ValidateCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that restaurant exists, is active, and is accepting orders.
    /// </summary>
    Task<Result> ValidateRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that restaurant is within operating hours.
    /// </summary>
    Task<Result> ValidateRestaurantHoursAsync(Guid restaurantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that menu items exist and are available.
    /// </summary>
    Task<Result> ValidateMenuItemsAsync(
        Guid restaurantId,
        IEnumerable<Guid> menuItemIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates delivery address is within service area.
    /// </summary>
    Task<Result> ValidateDeliveryAddressAsync(
        double latitude,
        double longitude,
        string pincode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates delivery address is within the restaurant's delivery radius using road distance.
    /// </summary>
    Task<Result> ValidateDeliveryDistanceAsync(
        double restaurantLat,
        double restaurantLon,
        double deliveryLat,
        double deliveryLon,
        CancellationToken cancellationToken = default);
}