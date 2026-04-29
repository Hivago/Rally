// File: src/Modules/Orders/RallyAPI.Orders.Application/Commands/RefundPayment/RefundPaymentCommand.cs

using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.SharedKernel;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.RefundPayment;

public record RefundPaymentCommand(
    Guid OrderId,
    decimal? Amount,           // null = full refund
    string? CallerRole = null, // "Admin" required to honor ForceRefund
    bool ForceRefund = false   // bypass payment.Status.IsRefundable() guard
) : IRequest<Result<RefundPaymentResponse>>;

public record RefundPaymentResponse(
    string RefundRequestId,
    string Status
);
