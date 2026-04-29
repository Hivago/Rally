

// ==========================================
// File: src/Modules/Orders/RallyAPI.Orders.Application/Commands/RefundPayment/RefundPaymentCommandHandler.cs

using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.SharedKernel;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Application.Commands.RefundPayment;

public class RefundPaymentCommandHandler
    : IRequestHandler<RefundPaymentCommand, Result<RefundPaymentResponse>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IPayUService _payUService;
    private readonly ILogger<RefundPaymentCommandHandler> _logger;

    public RefundPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IOrderRepository orderRepository,
        IPayUService payUService,
        ILogger<RefundPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _orderRepository = orderRepository;
        _payUService = payUService;
        _logger = logger;
    }

    public async Task<Result<RefundPaymentResponse>> Handle(
        RefundPaymentCommand command, CancellationToken ct)
    {
        // 1. Find payment
        var payment = await _paymentRepository.GetByOrderIdAsync(command.OrderId, ct);
        if (payment is null)
            return Result.Failure<RefundPaymentResponse>(
                Error.Create("Payment.NotFound", "No payment found for this order"));

        // 2. Check refundable (admin can bypass with ForceRefund)
        var bypassRefundableCheck = command.ForceRefund && command.CallerRole == "Admin";
        if (!bypassRefundableCheck && !payment.Status.IsRefundable())
            return Result.Failure<RefundPaymentResponse>(
                Error.Create("Payment.NotRefundable", $"Payment in {payment.Status} status cannot be refunded"));

        if (string.IsNullOrWhiteSpace(payment.PayuId))
            return Result.Failure<RefundPaymentResponse>(
                Error.Create("Payment.NoPayuId", "Cannot refund — no PayU transaction ID"));

        // 3. Determine refund amount
        var refundAmount = command.Amount ?? payment.Amount;
        if (refundAmount > payment.Amount)
            return Result.Failure<RefundPaymentResponse>(
                Error.Create("Payment.RefundExceedsAmount", "Refund amount exceeds payment amount"));

        // 4. Call PayU cancel_refund_transaction
        var uniqueToken = $"REFUND-{payment.OrderId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var result = await _payUService.RefundTransactionAsync(
            payment.PayuId, uniqueToken, refundAmount);

        if (result is null || result.Status != 1)
        {
            _logger.LogError("PayU refund API failed for Order {OrderId}: {Message}",
                command.OrderId, result?.Message ?? "null response");
            return Result.Failure<RefundPaymentResponse>(
                Error.Create("Payment.RefundFailed", result?.Message ?? "Refund request failed"));
        }

        // 5. Update payment record
        payment.MarkRefundInitiated(result.RequestId ?? uniqueToken, refundAmount);
        await _paymentRepository.UpdateAsync(payment, ct);

        // 6. Update order's payment status
        var order = await _orderRepository.GetByIdAsync(command.OrderId, ct);
        if (order is not null)
        {
            order.InitiateRefund();
            _orderRepository.Update(order, ct);
        }

        _logger.LogInformation(
            "Refund initiated for Order {OrderId}, Amount: {Amount}, RequestId: {RequestId}",
            command.OrderId, refundAmount, result.RequestId);

        return Result.Success(new RefundPaymentResponse(
            result.RequestId ?? uniqueToken, "Queued"));
    }
}