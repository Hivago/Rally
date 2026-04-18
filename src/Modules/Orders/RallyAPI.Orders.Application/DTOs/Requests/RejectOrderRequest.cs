namespace RallyAPI.Orders.Application.DTOs.Requests;

/// <summary>
/// Request model for rejecting an order.
/// </summary>
public sealed record RejectOrderRequest
{
    public string Reason { get; init; } = string.Empty;
}