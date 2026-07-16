using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Commands.UpdateOrderStatus;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.ValueObjects;

namespace RallyAPI.Orders.Application.Tests;

public class UpdateOrderStatusCustomerPickupTests
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateOrderStatusCommandHandler> _logger;
    private readonly UpdateOrderStatusCommandHandler _handler;

    public UpdateOrderStatusCustomerPickupTests()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _logger = Substitute.For<ILogger<UpdateOrderStatusCommandHandler>>();
        _handler = new UpdateOrderStatusCommandHandler(_orderRepository, _unitOfWork, _logger);
    }

    [Fact]
    public async Task Handle_WhenOwningRestaurantCompletesPickupOrder_ShouldMarkDelivered()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildReadyForPickupPickupOrder(restaurantId);
        var command = new UpdateOrderStatusCommand
        {
            OrderId = order.Id,
            TargetStatus = OrderStatus.Delivered,
            ActorId = restaurantId,
            ActorRole = "Restaurant"
        };

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNonOwningRestaurantCompletesPickupOrder_ShouldReturnUnauthorized()
    {
        var order = BuildReadyForPickupPickupOrder(restaurantId: Guid.NewGuid());
        var command = new UpdateOrderStatusCommand
        {
            OrderId = order.Id,
            TargetStatus = OrderStatus.Delivered,
            ActorId = Guid.NewGuid(), // different restaurant
            ActorRole = "Restaurant"
        };

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAdminCompletesPickupOrder_ShouldMarkDelivered()
    {
        var order = BuildReadyForPickupPickupOrder(restaurantId: Guid.NewGuid());
        var command = new UpdateOrderStatusCommand
        {
            OrderId = order.Id,
            TargetStatus = OrderStatus.Delivered,
            ActorId = Guid.NewGuid(),
            ActorRole = "Admin"
        };

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    #region Helpers

    private static Order BuildReadyForPickupPickupOrder(Guid restaurantId)
    {
        var pricing = OrderPricing.CreateSimple(subTotal: 300m, deliveryFee: 0m);

        var order = Order.CreatePendingOrder(
            orderNumber: OrderNumber.Create(dailySequence: 7),
            customerId: Guid.NewGuid(),
            customerName: "Priya Singh",
            restaurantId: restaurantId,
            restaurantName: "Dosa Corner",
            pricing: pricing,
            fulfillmentType: FulfillmentType.Pickup,
            deliveryInfo: null);

        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Test Item",
            Money.FromDecimal(300m, "INR"), 1));
        order.ConfirmPayment("PAY-TEST-PICKUP", null);
        order.Confirm();
        order.StartPreparing();
        order.MarkReadyForPickup();
        return order;
    }

    #endregion
}
