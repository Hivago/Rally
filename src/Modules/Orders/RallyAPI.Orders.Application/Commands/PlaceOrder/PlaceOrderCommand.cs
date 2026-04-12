using MediatR;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.DTOs.Requests;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.PlaceOrder;

/// <summary>
/// Command to create a pending order awaiting payment.
/// Payment is handled separately via InitiatePayment + PayU webhook.
/// </summary>
public sealed record PlaceOrderCommand : IRequest<Result<OrderDto>>
{
    // Customer info
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerPhone { get; init; }
    public string? CustomerEmail { get; init; }

    // Delivery quote (from Delivery Module)
    public string? DeliveryQuoteId { get; init; }

    // Request data
    public PlaceOrderRequest Request { get; init; } = new();

    public static PlaceOrderCommand Create(
        Guid customerId,
        string customerName,
        PlaceOrderRequest request,
        string? deliveryQuoteId = null,
        string? customerPhone = null,
        string? customerEmail = null)
    {
        return new PlaceOrderCommand
        {
            CustomerId = customerId,
            CustomerName = customerName,
            DeliveryQuoteId = deliveryQuoteId,
            CustomerPhone = customerPhone,
            CustomerEmail = customerEmail,
            Request = request
        };
    }
}