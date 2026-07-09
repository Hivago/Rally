using FluentAssertions;
using Xunit;
using NSubstitute;
using RallyAPI.Orders.Application.Queries.GetKitchenTicket;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.ValueObjects;

namespace RallyAPI.Orders.Application.Tests;

public class GetKitchenTicketQueryHandlerTests
{
    private readonly IOrderRepository _orderRepository;
    private readonly GetKitchenTicketQueryHandler _handler;

    public GetKitchenTicketQueryHandlerTests()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _handler = new GetKitchenTicketQueryHandler(_orderRepository);
    }

    [Fact]
    public async Task Handle_WhenOwningRestaurantRequests_ShouldReturnTicketWithItemsAndNotes()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidOrder(restaurantId, orderNote: "No onions in anything");
        var query = new GetKitchenTicketQuery(order.Id, restaurantId, "Restaurant");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ticket = result.Value;
        ticket.OrderNumber.Should().Be(order.OrderNumber.Value);
        ticket.CustomerName.Should().Be("Priya Singh");
        ticket.SpecialInstructions.Should().Be("No onions in anything");
        ticket.TotalItems.Should().Be(3); // 2 + 1
        ticket.Items.Should().HaveCount(2);
        ticket.Items[0].ItemName.Should().Be("Paneer Tikka");
        ticket.Items[0].Quantity.Should().Be(2);
        ticket.Items[0].SpecialInstructions.Should().Be("Extra spicy");
        ticket.Items[1].SpecialInstructions.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenCutleryRequested_ShouldFlagCutleryOnTicket()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidOrder(restaurantId, cutleryRequested: true);
        var query = new GetKitchenTicketQuery(order.Id, restaurantId, "Restaurant");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CutleryRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCutleryNotRequested_ShouldDefaultToFalse()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidOrder(restaurantId);
        var query = new GetKitchenTicketQuery(order.Id, restaurantId, "Restaurant");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CutleryRequested.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForDeliveryOrder_ShouldDisplayDelivery()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidOrder(restaurantId);
        var query = new GetKitchenTicketQuery(order.Id, restaurantId, "Restaurant");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FulfillmentType.Should().Be(FulfillmentType.Delivery);
        result.Value.FulfillmentDisplay.Should().Be("DELIVERY");
    }

    [Fact]
    public async Task Handle_ForPickupOrder_ShouldDisplayPickup()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidPickupOrder(restaurantId);
        var query = new GetKitchenTicketQuery(order.Id, restaurantId, "Restaurant");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FulfillmentType.Should().Be(FulfillmentType.Pickup);
        result.Value.FulfillmentDisplay.Should().Be("PICKUP");
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnFailure()
    {
        var orderId = Guid.NewGuid();
        var query = new GetKitchenTicketQuery(orderId, Guid.NewGuid(), "Restaurant");

        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns((Order?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenDifferentRestaurantRequests_ShouldReturnFailure()
    {
        var order = BuildPaidOrder(restaurantId: Guid.NewGuid());
        var query = new GetKitchenTicketQuery(order.Id, Guid.NewGuid(), "Restaurant");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenAdminRequests_ShouldReturnTicketForAnyRestaurant()
    {
        var order = BuildPaidOrder(restaurantId: Guid.NewGuid());
        // Admin caller id is unrelated to the order's restaurant
        var query = new GetKitchenTicketQuery(order.Id, Guid.NewGuid(), "Admin");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Rider")]
    [InlineData("")]
    public async Task Handle_WhenNonRestaurantNonAdminRequests_ShouldReturnFailure(string role)
    {
        var order = BuildPaidOrder(restaurantId: Guid.NewGuid());
        // Even if the caller id happens to match, only Restaurant/Admin roles are allowed
        var query = new GetKitchenTicketQuery(order.Id, order.RestaurantId, role);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    #region Helpers

    private static Order BuildPaidOrder(Guid restaurantId, string? orderNote = null, bool cutleryRequested = false)
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
            pricing: pricing,
            deliveryInfo: deliveryInfo,
            specialInstructions: orderNote,
            cutleryRequested: cutleryRequested);

        AddSampleItems(order);
        order.ConfirmPayment("PAY-TEST-001", null);
        return order;
    }

    private static Order BuildPaidPickupOrder(Guid restaurantId)
    {
        var pricing = OrderPricing.CreateSimple(subTotal: 300m, deliveryFee: 0m);

        var order = Order.CreatePendingOrder(
            orderNumber: OrderNumber.Create(dailySequence: 43),
            customerId: Guid.NewGuid(),
            customerName: "Priya Singh",
            restaurantId: restaurantId,
            restaurantName: "Dosa Corner",
            pricing: pricing,
            fulfillmentType: FulfillmentType.Pickup);

        AddSampleItems(order);
        order.ConfirmPayment("PAY-TEST-002", null);
        return order;
    }

    private static void AddSampleItems(Order order)
    {
        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Paneer Tikka",
            Money.FromDecimal(200m, "INR"), 2,
            specialInstructions: "Extra spicy"));

        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Butter Naan",
            Money.FromDecimal(50m, "INR"), 1));
    }

    #endregion
}
