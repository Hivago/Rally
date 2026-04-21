using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.Mappings;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.RejectOrder;

public sealed class RejectOrderCommandHandler : IRequestHandler<RejectOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RejectOrderCommandHandler> _logger;

    public RejectOrderCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<RejectOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OrderDto>> Handle(RejectOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(OrderErrors.NotFound(command.OrderId));
        }

        // Verify restaurant owns this order
        if (order.RestaurantId != command.RestaurantId)
        {
            return Result.Failure<OrderDto>(OrderErrors.NotRestaurantOrder);
        }

        // Check if can be rejected
        if (!order.Status.CanBeRejected())
        {
            return Result.Failure<OrderDto>(
                OrderErrors.CannotRejectInStatus(order.Status.GetDisplayName()));
        }

        try
        {
            order.Reject(command.Reason);

            // Initiate refund (Payments Module will handle actual refund)
            order.InitiateRefund();

            _orderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderNumber} rejected by restaurant {RestaurantId}. Reason: {Reason}",
                order.OrderNumber.Value,
                command.RestaurantId,
                command.Reason);

            return Result.Success(order.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to reject order {OrderId}: {Message}", command.OrderId, ex.Message);
            return Result.Failure<OrderDto>(OrderErrors.CannotRejectInStatus(order.Status.GetDisplayName()));
        }
    }
}