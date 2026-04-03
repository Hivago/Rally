using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Abstractions.Restaurants;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// When an order is delivered, create a PayoutLedger entry recording
/// the restaurant's earnings and Rally's commission for that order.
/// </summary>
public sealed class OrderDeliveredPayoutLedgerHandler : INotificationHandler<OrderDeliveredEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPayoutLedgerRepository _ledgerRepository;
    private readonly IRestaurantQueryService _restaurantQueryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderDeliveredPayoutLedgerHandler> _logger;

    public OrderDeliveredPayoutLedgerHandler(
        IOrderRepository orderRepository,
        IPayoutLedgerRepository ledgerRepository,
        IRestaurantQueryService restaurantQueryService,
        IUnitOfWork unitOfWork,
        ILogger<OrderDeliveredPayoutLedgerHandler> logger)
    {
        _orderRepository = orderRepository;
        _ledgerRepository = ledgerRepository;
        _restaurantQueryService = restaurantQueryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(OrderDeliveredEvent notification, CancellationToken cancellationToken)
    {
        // Check if ledger entry already exists (idempotency)
        var existing = await _ledgerRepository.GetByOrderIdAsync(notification.OrderId, cancellationToken);
        if (existing is not null)
        {
            _logger.LogWarning(
                "Payout ledger entry already exists for order {OrderId}, skipping",
                notification.OrderId);
            return;
        }

        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);
        if (order is null)
        {
            _logger.LogError("Order {OrderId} not found for payout ledger creation", notification.OrderId);
            return;
        }

        var restaurant = await _restaurantQueryService.GetByIdAsync(order.RestaurantId, cancellationToken);
        if (restaurant is null)
        {
            _logger.LogError(
                "Restaurant {RestaurantId} not found for payout ledger creation (order {OrderId})",
                order.RestaurantId, notification.OrderId);
            return;
        }

        if (restaurant.OwnerId is null)
        {
            _logger.LogWarning(
                "Restaurant {RestaurantId} has no owner, skipping payout ledger for order {OrderId}",
                order.RestaurantId, notification.OrderId);
            return;
        }

        try
        {
            // Use SubTotal as the order amount for commission calculation
            // (delivery fee, tips, service fees are not part of restaurant's earnings)
            var orderAmount = order.Pricing.SubTotal.Amount;

            var ledgerEntry = PayoutLedger.Create(
                ownerId: restaurant.OwnerId.Value,
                outletId: restaurant.Id,
                orderId: order.Id,
                orderAmount: orderAmount,
                commissionPercentage: restaurant.CommissionPercentage);

            await _ledgerRepository.AddAsync(ledgerEntry, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created payout ledger entry for order {OrderNumber}: amount={Amount}, commission={Commission}%, net={Net}",
                notification.OrderNumber, orderAmount, restaurant.CommissionPercentage, ledgerEntry.NetAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create payout ledger entry for order {OrderId}",
                notification.OrderId);
        }
    }
}
