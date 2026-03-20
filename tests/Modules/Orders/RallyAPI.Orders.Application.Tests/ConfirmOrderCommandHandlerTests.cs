using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Commands.ConfirmOrder;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.ValueObjects;

namespace RallyAPI.Orders.Application.Tests;

public class ConfirmOrderCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConfirmOrderCommandHandler> _logger;
    private readonly ConfirmOrderCommandHandler _handler;

    public ConfirmOrderCommandHandlerTests()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _logger = Substitute.For<ILogger<ConfirmOrderCommandHandler>>();
        _handler = new ConfirmOrderCommandHandler(_orderRepository, _unitOfWork, _logger);
    }

    [Fact]
    public async Task Handle_WhenOrderExistsAndRestaurantMatches_ShouldConfirmAndReturnSuccess()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidOrder(restaurantId);
        var command = new ConfirmOrderCommand(order.Id, restaurantId);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        order.Status.Should().Be(OrderStatus.Confirmed);
        _orderRepository.Received(1).Update(order);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnFailure()
    {
        var orderId = Guid.NewGuid();
        var command = new ConfirmOrderCommand(orderId, Guid.NewGuid());

        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns((Order?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenRestaurantDoesNotOwnOrder_ShouldReturnFailure()
    {
        var order = BuildPaidOrder(restaurantId: Guid.NewGuid());
        var differentRestaurantId = Guid.NewGuid();
        var command = new ConfirmOrderCommand(order.Id, differentRestaurantId);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOrderIsNotInPaidStatus_ShouldReturnFailure()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidOrder(restaurantId);
        order.Confirm(); // advance past Paid
        var command = new ConfirmOrderCommand(order.Id, restaurantId);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOrderIsCancelled_ShouldReturnFailure()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidOrder(restaurantId);
        order.Cancel(CancellationReason.CustomerRequested);
        var command = new ConfirmOrderCommand(order.Id, restaurantId);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    #region Helpers

    private static Order BuildPaidOrder(Guid restaurantId)
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

        return Order.CreatePaidOrder(
            orderNumber: OrderNumber.Create(dailySequence: 42),
            customerId: Guid.NewGuid(),
            customerName: "Priya Singh",
            restaurantId: restaurantId,
            restaurantName: "Dosa Corner",
            deliveryInfo: deliveryInfo,
            pricing: pricing,
            paymentId: "PAY-TEST-001");
    }

    #endregion
}
