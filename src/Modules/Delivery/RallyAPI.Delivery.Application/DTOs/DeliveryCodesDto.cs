namespace RallyAPI.Delivery.Application.DTOs;

/// <summary>
/// OTP codes used to verify the rider at pickup and the customer at drop.
/// Role-filtered: customers never see the pickup code, restaurants never see
/// the drop code. Admins receive both.
/// </summary>
public sealed record DeliveryCodesDto
{
    /// <summary>
    /// 6-digit code shown to the restaurant. Restaurant reads it out to the
    /// rider at pickup; the rider enters it in ProRouting to prove identity.
    /// </summary>
    public string? PickupCode { get; init; }

    /// <summary>
    /// 4-digit code shown to the customer. Customer reads it out to the rider
    /// at drop; the rider enters it in ProRouting to mark delivered.
    /// </summary>
    public string? DropCode { get; init; }
}
