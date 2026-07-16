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

        // Verify caller owns this order for the given transition
        if (!IsAuthorized(order, command.TargetStatus, command.ActorId, command.ActorRole))
        {
            return Result.Failure<OrderDto>(OrderErrors.Unauthorized);
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

    private static bool IsAuthorized(
        Domain.Entities.Order order,
        OrderStatus targetStatus,
        Guid? actorId,
        string? actorRole)
    {
        if (actorRole == "Admin") return true;

        return targetStatus switch
        {
            OrderStatus.Preparing or OrderStatus.ReadyForPickup =>
                actorRole == "Restaurant" && order.RestaurantId == actorId,

            // Pickup orders are collected by the customer and completed by the
            // owning restaurant (no rider, no DeliveryInfo).
            OrderStatus.Delivered when order.FulfillmentType == FulfillmentType.Pickup =>
                actorRole == "Restaurant" && order.RestaurantId == actorId,

            OrderStatus.PickedUp or OrderStatus.Delivered =>
                actorRole == "Rider" && order.DeliveryInfo?.RiderId == actorId,

            _ => false
        };
    }
}