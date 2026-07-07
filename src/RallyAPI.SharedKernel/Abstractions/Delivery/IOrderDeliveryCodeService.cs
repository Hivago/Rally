namespace RallyAPI.SharedKernel.Abstractions.Delivery;

/// <summary>
/// Cross-module read for the delivery handoff codes tied to an order.
/// Implemented by the Delivery module, consumed by Orders (e.g. the customer bill/label
/// prints the delivery OTP). Returns null when no delivery request exists yet (e.g. an
/// unconfirmed or pickup order).
/// </summary>
public interface IOrderDeliveryCodeService
{
    Task<OrderDeliveryCodes?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>
/// Handoff OTPs for a delivery.
/// PickupCode = shown to the rider at the restaurant on pickup.
/// DropCode   = shared with the customer to confirm delivery.
/// </summary>
public sealed record OrderDeliveryCodes(string? PickupCode, string? DropCode);
