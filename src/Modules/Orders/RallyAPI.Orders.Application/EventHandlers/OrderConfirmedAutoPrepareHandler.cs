using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Events;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Auto-promotes Confirmed → Preparing so the restaurant only ever has to
/// press a single "Accept" button. The flow is now:
///   Paid → Confirmed (manual Accept or AutoAccept)
///       → Preparing  (this handler, fires on OrderConfirmedEvent)
///
/// Confirmed remains a real, persisted state — it just lives for a moment
/// before the kitchen-side Preparing transition follows. This gives downstream
/// consumers (delivery dispatch via OrderConfirmedIntegrationEvent, customer
/// "Confirmed" push) a clean signal point and lets us keep Reject available
/// from Confirmed for restaurants that change their mind during that window.
/// </summary>
public sealed class OrderConfirmedAutoPrepareHandler : INotificationHandler<OrderConfirmedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderConfirmedAutoPrepareHandler> _logger;

    public OrderConfirmedAutoPrepareHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderConfirmedAutoPrepareHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(OrderConfirmedEvent notification, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning(
                "OrderConfirmedAutoPrepareHandler: order {OrderId} not found, skipping auto-prepare",
                notification.OrderId);
            return;
        }

        // Only promote if still Confirmed. If the order has already moved on
        // (e.g. another handler advanced it, or it was rejected during the
        // brief Confirmed window) leave it alone.
        if (order.Status != OrderStatus.Confirmed)
        {
            return;
        }

        try
        {
            order.StartPreparing();
            _orderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderNumber} auto-promoted (Confirmed → Preparing)",
                notification.OrderNumber);
        }
        catch (Exception ex)
        {
            // Don't bubble — the order is safely Confirmed; a manual
            // /preparing call can recover it.
            _logger.LogError(ex,
                "Failed to auto-promote order {OrderNumber} from Confirmed to Preparing",
                notification.OrderNumber);
        }
    }
}
