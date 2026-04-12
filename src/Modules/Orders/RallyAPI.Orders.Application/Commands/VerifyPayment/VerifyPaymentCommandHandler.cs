
// ==========================================
// File: src/Modules/Orders/RallyAPI.Orders.Application/Commands/VerifyPayment/VerifyPaymentCommandHandler.cs

using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.SharedKernel;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.VerifyPayment;

public class VerifyPaymentCommandHandler
    : IRequestHandler<VerifyPaymentCommand, Result<VerifyPaymentResponse>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayUService _payUService;
    private readonly ILogger<VerifyPaymentCommandHandler> _logger;

    public VerifyPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IPayUService payUService,
        ILogger<VerifyPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _payUService = payUService;
        _logger = logger;
    }

    public async Task<Result<VerifyPaymentResponse>> Handle(
        VerifyPaymentCommand command, CancellationToken ct)
    {
        // 1. Find payment
        var payment = await _paymentRepository.GetByTxnIdAsync(command.TxnId, ct);
        if (payment is null)
            return Result.Failure<VerifyPaymentResponse>(
                Error.Create("Payment.NotFound", "Payment not found"));

        // 2. Verify customer owns this payment
        if (payment.CustomerId != command.CustomerId)
            return Result.Failure<VerifyPaymentResponse>(
                Error.Create("Payment.Unauthorized", "Unauthorized"));

        // 3. If already resolved locally, return cached status
        if (payment.Status == Domain.Enums.PaymentStatus.Paid)
        {
            return Result.Success(new VerifyPaymentResponse(
                "success", payment.Amount.ToString("F2"),
                payment.PayuId, payment.PaymentMode));
        }

        // 4. Call PayU verify_payment API
        var result = await _payUService.VerifyPaymentAsync(command.TxnId);
        if (result is null)
            return Result.Failure<VerifyPaymentResponse>(
                Error.Create("Payment.VerificationFailed", "Could not verify payment with PayU"));

        // 5. If PayU says success but we haven't updated yet (webhook delayed), update now
        if (string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase)
            && payment.Status != Domain.Enums.PaymentStatus.Paid)
        {
            payment.MarkSuccess(result.PayuId, result.Mode ?? "", result.BankRefNum);

            // Also transition order Pending → Paid (safety net for delayed/lost webhooks)
            var order = await _orderRepository.GetByIdAsync(payment.OrderId, ct);
            if (order is not null && order.Status == Domain.Enums.OrderStatus.Pending)
            {
                order.ConfirmPayment(payment.TxnId, result.PayuId);
                _logger.LogInformation(
                    "Order {OrderId} transitioned Pending → Paid via verify API for TxnId {TxnId}",
                    payment.OrderId, command.TxnId);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Payment verified as SUCCESS via API for TxnId {TxnId} (webhook may have been delayed)",
                command.TxnId);
        }

        return Result.Success(new VerifyPaymentResponse(
            result.Status, result.Amount, result.PayuId, result.Mode));
    }
}