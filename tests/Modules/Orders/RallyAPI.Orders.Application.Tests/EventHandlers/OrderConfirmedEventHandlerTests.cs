using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.EventHandlers;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Events;
using RallyAPI.Orders.Domain.ValueObjects;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;
using Xunit;

namespace RallyAPI.Orders.Application.Tests.EventHandlers;

public class OrderConfirmedEventHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IOutboxWriter _outbox = Substitute.For<IOutboxWriter>();
    private readonly ILogger<OrderConfirmedEventHandler> _logger =
        Substitute.For<ILogger<OrderConfirmedEventHandler>>();

    private readonly OrderConfirmedEventHandler _handler;

    public OrderConfirmedEventHandlerTests()
    {
        _handler = new OrderConfirmedEventHandler(_orderRepository, _outbox, _logger);
    }

    [Fact]
    public async Task Handle_WhenDeliveryOrder_WritesIntegrationEventToOutbox()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildConfirmedDeliveryOrder(restaurantId);
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var evt = new OrderConfirmedEvent(order.Id, order.OrderNumber.Value, restaurantId, order.CustomerId);

        await _handler.Handle(evt, CancellationToken.None);

        // The bridge must persist the integration event to the outbox (durable),
        // not publish it in-process (where a consumer failure would lose it).
        await _outbox.Received(1).WriteAsync(
            Arg.Is<OrderConfirmedIntegrationEvent>(e =>
                e.OrderId == order.Id &&
                e.IsPickupOrder == false &&
                e.DropPincode == "560025"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_DoesNotWriteToOutbox()
    {
        _orderRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Order?)null);

        var evt = new OrderConfirmedEvent(Guid.NewGuid(), "ORD-1", Guid.NewGuid(), Guid.NewGuid());

        await _handler.Handle(evt, CancellationToken.None);

        await _outbox.DidNotReceive().WriteAsync(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    private static Order BuildConfirmedDeliveryOrder(Guid restaurantId)
    {
        var deliveryAddress = Address.Create(
            street: "42 Brigade Road",
            city: "Bengaluru",
            pincode: "560025",
            latitude: 12.9716,
            longitude: 77.5946);

        var deliveryInfo = DeliveryInfo.Create(
            pickupLatitude: 12.9352,
            pickupLongitude: 77.6245,
            pickupPincode: "560095",
            deliveryAddress: deliveryAddress);

        var pricing = OrderPricing.CreateSimple(subTotal: 300m, deliveryFee: 50m);

        var order = Order.CreatePendingOrder(
            orderNumber: OrderNumber.Create(dailySequence: 42),
            customerId: Guid.NewGuid(),
            customerName: "Priya Singh",
            restaurantId: restaurantId,
            restaurantName: "Dosa Corner",
            deliveryInfo: deliveryInfo,
            pricing: pricing);

        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Masala Dosa",
            Money.FromDecimal(300m, "INR"), 1));
        order.ConfirmPayment("PAY-TEST-001", null);
        order.Confirm();
        return order;
    }
}
