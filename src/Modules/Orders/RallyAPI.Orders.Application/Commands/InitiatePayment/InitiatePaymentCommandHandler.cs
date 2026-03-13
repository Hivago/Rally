// File: src/Modules/Orders/RallyAPI.Orders.Application/Commands/InitiatePayment/InitiatePaymentCommandHandler.cs

using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.InitiatePayment;

public class InitiatePaymentCommandHandler
    : IRequestHandler<InitiatePaymentCommand, Result<InitiatePaymentResponse>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPayUService _payUService;
    private readonly ILogger<InitiatePaymentCommandHandler> _logger;

    public InitiatePaymentCommandHandler(
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        IPayUService payUService,
        ILogger<InitiatePaymentCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _payUService = payUService;
        _logger = logger;
    }

    public async Task<Result<InitiatePaymentResponse>> Handle(
        InitiatePaymentCommand command, CancellationToken ct)
    {
        // 1. Get the order
        var order = await _orderRepository.GetByIdAsync(command.OrderId, ct);
        if (order is null)
            return Result.Failure<InitiatePaymentResponse>(
                Error.Create("Payment.OrderNotFound", "Order not found"));

        // 2. Verify the customer owns this order
        if (order.CustomerId != command.CustomerId)
            return Result.Failure<InitiatePaymentResponse>(
                Error.Create("Payment.Unauthorized", "You can only pay for your own orders"));

        // 3. Check if payment already exists for this order
        var existingPayment = await _paymentRepository.GetByOrderIdAsync(command.OrderId, ct);
        if (existingPayment is not null)
        {
            if (existingPayment.Status == Domain.Enums.PaymentStatus.Paid)
                return Result.Failure<InitiatePaymentResponse>(
                    Error.Create("Payment.AlreadyPaid", "This order has already been paid"));

            // If previous payment failed, allow retry — generate new params for existing record
            if (existingPayment.Status == Domain.Enums.PaymentStatus.Failed)
            {
                // Create new payment record for retry
                _logger.LogInformation("Previous payment failed for Order {OrderId}, creating retry payment", command.OrderId);
            }
            else if (existingPayment.Status == Domain.Enums.PaymentStatus.Processing ||
                     existingPayment.Status == Domain.Enums.PaymentStatus.Pending)
            {
                // Return existing checkout params
                var existingParams = _payUService.GenerateCheckoutParams(
                    existingPayment.TxnId,
                    existingPayment.Amount,
                    $"Rally Order {order.OrderNumber}",
                    existingPayment.CustomerName,
                    existingPayment.CustomerEmail,
                    existingPayment.CustomerPhone);

                existingPayment.MarkInitiated();
                await _paymentRepository.UpdateAsync(existingPayment, ct);

                return Result.Success(MapToResponse(existingParams));
            }
        }

        // 4. Generate unique transaction ID
        var txnId = $"RALLY-{order.OrderNumber}-{DateTime.UtcNow:HHmmss}";

        // 5. Create Payment record
        var payment = Payment.Create(
            orderId: command.OrderId,
            customerId: command.CustomerId,
            txnId: txnId,
            amount: order.Pricing.Total.Amount,
            customerName: order.CustomerName ?? "Customer",
            customerEmail: !string.IsNullOrWhiteSpace(order.CustomerEmail)
            ? order.CustomerEmail
            : $"{order.CustomerPhone.TrimStart('+')}@rally.app",
            customerPhone: order.CustomerPhone);

        payment.MarkInitiated();
        await _paymentRepository.AddAsync(payment, ct);

        _logger.LogInformation(
            "Payment initiated for Order {OrderId}, TxnId: {TxnId}, Amount: {Amount}",
            command.OrderId, txnId, payment.Amount);

        // 6. Generate PayU checkout params with server-side hash
        var checkoutParams = _payUService.GenerateCheckoutParams(
            txnId,
            payment.Amount,
            $"Rally Order {order.OrderNumber}",
            payment.CustomerName,
            payment.CustomerEmail,
            payment.CustomerPhone);

        return Result.Success(MapToResponse(checkoutParams));
    }

    private static InitiatePaymentResponse MapToResponse(PayUCheckoutParams p)
        => new(p.Key, p.TxnId, p.Amount, p.ProductInfo,
               p.FirstName, p.Email, p.Phone, p.Surl, p.Furl,
               p.Hash, p.PayUBaseUrl);
}