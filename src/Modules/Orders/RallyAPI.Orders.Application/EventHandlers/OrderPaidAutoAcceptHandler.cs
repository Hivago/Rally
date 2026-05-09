using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.SharedKernel.Abstractions.Restaurants;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// When an order is paid, check if the restaurant has auto-accept enabled.
/// If so, immediately move the order into Preparing (collapsed single-step accept flow)
/// without waiting for the restaurant to manually press Accept.
/// </summary>
public sealed class OrderPaidAutoAcceptHandler : INotificationHandler<OrderPaidEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRestaurantQueryService _restaurantQueryService;
    private readonly ILogger<OrderPaidAutoAcceptHandler> _logger;

    public OrderPaidAutoAcceptHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IRestaurantQueryService restaurantQueryService,
        ILogger<OrderPaidAutoAcceptHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _restaurantQueryService = restaurantQueryService;
        _logger = logger;
    }

    public async Task Handle(OrderPaidEvent notification, CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantQueryService.GetByIdAsync(
            notification.RestaurantId, cancellationToken);

        if (restaurant is null || !restaurant.AutoAcceptOrders)
            return;

        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);

        if (order is null || order.Status != OrderStatus.Paid)
            return;

        try
        {
            // StartPreparing handles Paid → Preparing directly and also raises
            // OrderConfirmedEvent so delivery dispatch + customer "confirmed" push fire.
            order.StartPreparing();
            _orderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderNumber} auto-accepted (Paid → Preparing) for restaurant {RestaurantId}",
                notification.OrderNumber, notification.RestaurantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to auto-accept order {OrderNumber} for restaurant {RestaurantId}",
                notification.OrderNumber, notification.RestaurantId);
        }
    }
}
