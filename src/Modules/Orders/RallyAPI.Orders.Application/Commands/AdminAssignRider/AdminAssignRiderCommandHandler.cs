using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.AdminAssignRider;

public sealed class AdminAssignRiderCommandHandler : IRequestHandler<AdminAssignRiderCommand, Result>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IRiderQueryService _riderQueryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AdminAssignRiderCommandHandler> _logger;

    public AdminAssignRiderCommandHandler(
        IOrderRepository orderRepository,
        IRiderQueryService riderQueryService,
        IUnitOfWork unitOfWork,
        ILogger<AdminAssignRiderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _riderQueryService = riderQueryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(AdminAssignRiderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure(OrderErrors.NotFound(command.OrderId));

        if (order.FulfillmentType == FulfillmentType.Pickup)
            return Result.Failure(Error.Validation("Pickup orders cannot have a rider assigned."));

        if (!order.Status.IsActive())
            return Result.Failure(OrderErrors.CannotModifyInStatus(order.Status.GetDisplayName()));

        var rider = await _riderQueryService.GetRiderByIdAsync(command.RiderId, cancellationToken);
        if (rider is null)
            return Result.Failure(OrderErrors.RiderNotFound(command.RiderId));

        if (!rider.IsActive)
            return Result.Failure(Error.Validation("Cannot assign an inactive rider."));

        try
        {
            order.AssignRider(rider.RiderId, rider.Name, rider.Phone);

            _orderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Admin manually assigned rider {RiderId} ({RiderName}) to order {OrderNumber}",
                rider.RiderId, rider.Name, order.OrderNumber.Value);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Admin rider assign failed for order {OrderId}: {Message}",
                command.OrderId, ex.Message);
            return Result.Failure(Error.Validation(ex.Message));
        }
    }
}
