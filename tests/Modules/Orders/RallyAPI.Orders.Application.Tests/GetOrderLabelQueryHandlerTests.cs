using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Options;
using NSubstitute;
using RallyAPI.Orders.Application.Options;
using RallyAPI.Orders.Application.Queries.GetOrderLabel;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.ValueObjects;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.Abstractions.Restaurants;

namespace RallyAPI.Orders.Application.Tests;

public class GetOrderLabelQueryHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantQueryService _restaurantQueryService = Substitute.For<IRestaurantQueryService>();
    private readonly IOrderDeliveryCodeService _deliveryCodeService = Substitute.For<IOrderDeliveryCodeService>();
    private readonly GetOrderLabelQueryHandler _handler;

    public GetOrderLabelQueryHandlerTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new PlatformOptions { FssaiNumber = "PLATFORM-FSSAI-123" });
        _handler = new GetOrderLabelQueryHandler(
            _orderRepository, _restaurantQueryService, _deliveryCodeService, options);
    }

    [Fact]
    public async Task Handle_WhenOwningRestaurantRequests_ShouldReturnLabelWithPricingFssaiAndOtp()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidDeliveryOrder(restaurantId);
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _restaurantQueryService.GetByIdAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns(BuildRestaurantDetails(restaurantId, fssai: "REST-FSSAI-999"));
        _deliveryCodeService.GetByOrderIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(new OrderDeliveryCodes(PickupCode: "1937", DropCode: "4242"));

        var result = await _handler.Handle(
            new GetOrderLabelQuery(order.Id, restaurantId, "Restaurant"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var label = result.Value;
        label.Total.Should().Be(350m);          // subtotal 300 + delivery 50
        label.SubTotal.Should().Be(300m);
        label.DeliveryFee.Should().Be(50m);
        label.RestaurantFssai.Should().Be("REST-FSSAI-999");
        label.PlatformFssai.Should().Be("PLATFORM-FSSAI-123");
        label.DeliveryOtp.Should().Be("1937");   // pickup code
        label.DeliveryAddress.Should().NotBeNullOrEmpty();
        label.Items.Should().HaveCount(2);
        label.Items[0].LineTotal.Should().Be(400m); // 200 x 2
    }

    [Fact]
    public async Task Handle_WhenDifferentRestaurantRequests_ShouldReturnNotFound()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidDeliveryOrder(restaurantId);
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.Handle(
            new GetOrderLabelQuery(order.Id, Guid.NewGuid(), "Restaurant"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.NotFound");
    }

    [Fact]
    public async Task Handle_WhenAdminRequests_ShouldReturnLabel()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidDeliveryOrder(restaurantId);
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _restaurantQueryService.GetByIdAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns(BuildRestaurantDetails(restaurantId));
        _deliveryCodeService.GetByOrderIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns((OrderDeliveryCodes?)null);

        var result = await _handler.Handle(
            new GetOrderLabelQuery(order.Id, Guid.NewGuid(), "Admin"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ForPickupOrder_ShouldNotQueryDeliveryOtpAndHaveNoDeliveryAddress()
    {
        var restaurantId = Guid.NewGuid();
        var order = BuildPaidPickupOrder(restaurantId);
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _restaurantQueryService.GetByIdAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns(BuildRestaurantDetails(restaurantId));

        var result = await _handler.Handle(
            new GetOrderLabelQuery(order.Id, restaurantId, "Restaurant"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FulfillmentDisplay.Should().Be("PICKUP");
        result.Value.DeliveryOtp.Should().BeNull();
        result.Value.DeliveryAddress.Should().BeNull();
        await _deliveryCodeService.DidNotReceive()
            .GetByOrderIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #region Builders

    private static Order BuildPaidDeliveryOrder(Guid restaurantId)
    {
        var deliveryAddress = Address.Create(
            street: "CHS, Sector 9", city: "Navi Mumbai", pincode: "400708",
            latitude: 19.1568, longitude: 72.9990);

        var deliveryInfo = DeliveryInfo.Create(
            pickupLatitude: 19.15, pickupLongitude: 72.99,
            pickupPincode: "400708", deliveryAddress: deliveryAddress);

        var pricing = OrderPricing.CreateSimple(subTotal: 300m, deliveryFee: 50m, taxRate: 0m);

        var order = Order.CreatePendingOrder(
            orderNumber: OrderNumber.Create(dailySequence: 44),
            customerId: Guid.NewGuid(),
            customerName: "Aviral",
            restaurantId: restaurantId,
            restaurantName: "Annada",
            pricing: pricing,
            deliveryInfo: deliveryInfo,
            cutleryRequested: true);

        AddSampleItems(order);
        order.ConfirmPayment("PAY-LABEL-001", null);
        return order;
    }

    private static Order BuildPaidPickupOrder(Guid restaurantId)
    {
        var pricing = OrderPricing.CreateSimple(subTotal: 300m, deliveryFee: 0m);

        var order = Order.CreatePendingOrder(
            orderNumber: OrderNumber.Create(dailySequence: 45),
            customerId: Guid.NewGuid(),
            customerName: "Aviral",
            restaurantId: restaurantId,
            restaurantName: "Annada",
            pricing: pricing,
            fulfillmentType: FulfillmentType.Pickup);

        AddSampleItems(order);
        order.ConfirmPayment("PAY-LABEL-002", null);
        return order;
    }

    private static void AddSampleItems(Order order)
    {
        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Chicken Dum Biryani",
            Money.FromDecimal(200m, "INR"), 2, specialInstructions: "add onion"));
        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Butter Naan",
            Money.FromDecimal(50m, "INR"), 1));
    }

    private static RestaurantDetails BuildRestaurantDetails(Guid id, string? fssai = null) => new()
    {
        Id = id,
        Name = "Annada",
        Phone = "9880000000",
        AddressLine = "Airoli, Navi Mumbai",
        Latitude = 19.15,
        Longitude = 72.99,
        IsActive = true,
        IsAcceptingOrders = true,
        AutoAcceptOrders = false,
        AvgPrepTimeMins = 20,
        OpeningTime = new TimeOnly(9, 0),
        ClosingTime = new TimeOnly(23, 0),
        CommissionPercentage = 20m,
        CommissionFlatFee = 0m,
        FssaiNumber = fssai
    };

    #endregion
}
