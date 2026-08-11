using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Commands.RefundPayment;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.Orders.Domain.ValueObjects;

namespace RallyAPI.Orders.Application.Tests;

public class RefundPaymentCommandHandlerTests
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IPayUService _payUService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RefundPaymentCommandHandler> _logger;
    private readonly RefundPaymentCommandHandler _handler;

    public RefundPaymentCommandHandlerTests()
    {
        _paymentRepository = Substitute.For<IPaymentRepository>();
        _orderRepository = Substitute.For<IOrderRepository>();
        _payUService = Substitute.For<IPayUService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _logger = Substitute.For<ILogger<RefundPaymentCommandHandler>>();
        _handler = new RefundPaymentCommandHandler(
            _paymentRepository, _orderRepository, _payUService, _unitOfWork, _logger);
    }

    [Fact]
    public async Task Handle_WhenForceRefundOnStuckProcessingPaymentWithPayuId_ShouldReconcileToPaidAndInitiateRefund()
    {
        // Reproduces the 2026-07-15 incident: webhook never confirmed success, so the payment
        // is stuck in Processing even though PayU already has a PayuId on record for it.
        var order = BuildCancelledOrder(out var orderId);
        var payment = BuildProcessingPaymentWithPayuId(orderId, payuId: "29572291478");
        var command = new RefundPaymentCommand(orderId, Amount: null, CallerRole: "Admin", ForceRefund: true);

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(payment);
        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);
        _payUService.RefundTransactionAsync("29572291478", Arg.Any<string>(), payment.Amount)
            .Returns(new PayURefundResult { Status = 1, RequestId = "22135757954", Message = "Queued" });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.RefundInitiated);
        order.Status.Should().Be(OrderStatus.Refunding);
        await _paymentRepository.Received(1).UpdateAsync(payment, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenForceRefundButNoPayuIdRecorded_ShouldFailWithoutCallingPayU()
    {
        var order = BuildCancelledOrder(out var orderId);
        var payment = BuildProcessingPaymentWithPayuId(orderId, payuId: null);
        var command = new RefundPaymentCommand(orderId, Amount: null, CallerRole: "Admin", ForceRefund: true);

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(payment);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Payment.NoPayuId");
        await _payUService.DidNotReceive().RefundTransactionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>());
    }

    [Fact]
    public async Task Handle_WhenOrderStatusDoesNotRequireRefund_ShouldSkipOrderUpdateButStillSucceed()
    {
        // Payment is already legitimately Paid and refundable on its own; the order just
        // hasn't reached a refund-eligible status (e.g. still Paid, not yet Cancelled/Rejected).
        var order = BuildPaidOrderNotCancelled(out var orderId);
        var payment = BuildPaidPayment(orderId, payuId: "PAYU-OK");
        var command = new RefundPaymentCommand(orderId, Amount: null, CallerRole: null, ForceRefund: false);

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(payment);
        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);
        _payUService.RefundTransactionAsync("PAYU-OK", Arg.Any<string>(), payment.Amount)
            .Returns(new PayURefundResult { Status = 1, RequestId = "REQ-1", Message = "Queued" });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid); // unchanged — RequiresRefund() was false
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPayUReturnsFailure_ShouldReturnFailureAndNotMutatePayment()
    {
        var order = BuildCancelledOrder(out var orderId);
        var payment = BuildPaidPayment(orderId, payuId: "PAYU-OK");
        var command = new RefundPaymentCommand(orderId, Amount: null, CallerRole: null, ForceRefund: false);

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(payment);
        _payUService.RefundTransactionAsync("PAYU-OK", Arg.Any<string>(), payment.Amount)
            .Returns(new PayURefundResult { Status = 0, Message = "Refund FAILURE" });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        payment.Status.Should().Be(PaymentStatus.Paid);
        await _paymentRepository.DidNotReceive().UpdateAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    #region Helpers

    private static Order BuildCancelledOrder(out Guid orderId)
    {
        var order = CreatePendingOrder();
        order.Cancel(CancellationReason.PaymentTimeout);
        orderId = order.Id;
        return order;
    }

    private static Order BuildPaidOrderNotCancelled(out Guid orderId)
    {
        var order = CreatePendingOrder();
        order.ConfirmPayment("PAY-TEST-001", null);
        orderId = order.Id;
        return order;
    }

    private static Order CreatePendingOrder()
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

        var pricing = OrderPricing.CreateSimple(subTotal: 119.30m, deliveryFee: 50m);

        var order = Order.CreatePendingOrder(
            orderNumber: OrderNumber.Create(dailySequence: 273),
            customerId: Guid.NewGuid(),
            customerName: "Test Customer",
            restaurantId: Guid.NewGuid(),
            restaurantName: "Titos",
            deliveryInfo: deliveryInfo,
            pricing: pricing);

        order.AddItem(OrderItem.Create(
            Guid.NewGuid(), "Test Item",
            Money.FromDecimal(119.30m, "INR"), 1));

        return order;
    }

    private static Payment BuildProcessingPaymentWithPayuId(Guid orderId, string? payuId)
    {
        var payment = Payment.Create(
            orderId, Guid.NewGuid(), txnId: $"RALLY-ORD-TEST-{orderId:N}",
            amount: 169.30m, customerName: "Test Customer",
            customerEmail: "test@example.com", customerPhone: "8080125309");
        payment.MarkInitiated(); // Pending -> Processing

        if (payuId is not null)
        {
            // Simulate the real-world anomaly: PayU already has this transaction (payu_id
            // reconciled from the dashboard), but the webhook never arrived so the domain
            // status is still stuck at Processing — not reachable via any public transition.
            typeof(Payment).GetProperty(nameof(Payment.PayuId))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(payment, new object?[] { payuId });
        }

        return payment;
    }

    private static Payment BuildPaidPayment(Guid orderId, string payuId)
    {
        var payment = Payment.Create(
            orderId, Guid.NewGuid(), txnId: $"RALLY-ORD-TEST-{orderId:N}",
            amount: 169.30m, customerName: "Test Customer",
            customerEmail: "test@example.com", customerPhone: "8080125309");
        payment.MarkInitiated();
        payment.MarkSuccess(payuId, "CC", "BANKREF123");
        return payment;
    }

    #endregion
}
