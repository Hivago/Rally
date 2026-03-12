using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.Mappings;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateOrderStatusCommandHandler> _logger;

    public UpdateOrderStatusCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateOrderStatusCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OrderDto>> Handle(UpdateOrderStatusCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(OrderErrors.NotFound(command.OrderId));
        }

        if (!CanUpdateOrder(order, command))
        {
            return Result.Failure<OrderDto>(
                Error.Forbidden("You are not allowed to update this order."));
        }

        // Validate transition is allowed
        var validTransitions = order.GetValidTransitions();
        if (!validTransitions.Contains(command.TargetStatus))
        {
            return Result.Failure<OrderDto>(
                OrderErrors.InvalidStatusTransition(
                    order.Status.GetDisplayName(),
                    command.TargetStatus.GetDisplayName()));
        }

        try
        {
            // Apply the status transition
            switch (command.TargetStatus)
            {
                case OrderStatus.Confirmed:
                    order.Confirm();
                    break;
                case OrderStatus.Preparing:
                    order.StartPreparing();
                    break;
                case OrderStatus.ReadyForPickup:
                    order.MarkReadyForPickup();
                    break;
                case OrderStatus.PickedUp:
                    order.MarkPickedUp();
                    break;
                case OrderStatus.Delivered:
                    order.MarkDelivered();
                    break;
                default:
                    return Result.Failure<OrderDto>(
                        OrderErrors.InvalidStatusTransition(
                            order.Status.GetDisplayName(),
                            command.TargetStatus.GetDisplayName()));
            }

            _orderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderNumber} status updated to {Status} by {Actor}",
                order.OrderNumber.Value,
                command.TargetStatus.GetDisplayName(),
                command.ActorId);

            return Result.Success(order.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "Failed to update order {OrderId} to {Status}: {Message}",
                command.OrderId,
                command.TargetStatus,
                ex.Message);

            return Result.Failure<OrderDto>(
                OrderErrors.InvalidStatusTransition(
                    order.Status.GetDisplayName(),
                    command.TargetStatus.GetDisplayName()));
        }
    }

    private static bool CanUpdateOrder(
        Domain.Entities.Order order,
        UpdateOrderStatusCommand command)
    {
        if (string.Equals(command.ActorUserType, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!command.ActorId.HasValue || string.IsNullOrWhiteSpace(command.ActorUserType))
        {
            return false;
        }

        return command.TargetStatus switch
        {
            OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.ReadyForPickup =>
                string.Equals(command.ActorUserType, "restaurant", StringComparison.OrdinalIgnoreCase)
                && order.RestaurantId == command.ActorId.Value,

            OrderStatus.PickedUp or OrderStatus.Delivered =>
                string.Equals(command.ActorUserType, "rider", StringComparison.OrdinalIgnoreCase)
                && order.DeliveryInfo.RiderId == command.ActorId.Value,

            _ => false
        };
    }
}
