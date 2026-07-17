using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.Mappings;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.AssignRider;

public sealed class AssignRiderCommandHandler : IRequestHandler<AssignRiderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssignRiderCommandHandler> _logger;

    public AssignRiderCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<AssignRiderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OrderDto>> Handle(AssignRiderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(OrderErrors.NotFound(command.OrderId));
        }

        // Verify caller is Admin or the restaurant that owns this order
        var isAuthorized = command.AssignedByRole == "Admin"
            || (command.AssignedByRole == "Restaurant" && order.RestaurantId == command.AssignedById);

        if (!isAuthorized)
        {
            return Result.Failure<OrderDto>(OrderErrors.Unauthorized);
        }

        // Can only assign rider to active orders
        if (!order.Status.IsActive())
        {
            return Result.Failure<OrderDto>(
                OrderErrors.CannotModifyInStatus(order.Status.GetDisplayName()));
        }

        try
        {
            // Admin/restaurant manual assignment names a Rally rider by id — never a 3PL agent.
            order.UpdateRiderInfo(command.RiderId, isOwnFleet: true, command.RiderName, command.RiderPhone);

            _orderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Rider {RiderId} assigned to order {OrderNumber}",
                command.RiderId,
                order.OrderNumber.Value);

            return Result.Success(order.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign rider to order {OrderId}", command.OrderId);
            return Result.Failure<OrderDto>(OrderErrors.Unexpected(ex.Message));
        }
    }
}