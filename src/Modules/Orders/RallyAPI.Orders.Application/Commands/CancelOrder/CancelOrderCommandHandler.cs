using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.Mappings;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OrderDto>> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(OrderErrors.NotFound(command.OrderId));
        }

        if (!CanCancelOrder(order, command))
        {
            return Result.Failure<OrderDto>(
                Error.Forbidden("You are not allowed to cancel this order."));
        }

        if (!order.Status.CanBeCancelled())
        {
            return Result.Failure<OrderDto>(OrderErrors.CannotCancelInStatus(order.Status.GetDisplayName()));
        }

        try
        {
            order.Cancel(command.Reason, command.CancelledBy, command.Notes);

            _orderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderNumber} cancelled. Reason: {Reason}, By: {CancelledBy}",
                order.OrderNumber.Value,
                command.Reason,
                command.CancelledBy);

            return Result.Success(order.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to cancel order {OrderId}: {Message}", command.OrderId, ex.Message);
            return Result.Failure<OrderDto>(OrderErrors.CannotCancelInStatus(order.Status.GetDisplayName()));
        }
    }

    private static bool CanCancelOrder(
        Domain.Entities.Order order,
        CancelOrderCommand command)
    {
        if (string.Equals(command.CancelledByUserType, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return command.CancelledByUserType?.ToLowerInvariant() switch
        {
            "customer" => order.CustomerId == command.CancelledBy,
            "restaurant" => order.RestaurantId == command.CancelledBy,
            "rider" => order.DeliveryInfo.RiderId == command.CancelledBy,
            _ => false
        };
    }
}
