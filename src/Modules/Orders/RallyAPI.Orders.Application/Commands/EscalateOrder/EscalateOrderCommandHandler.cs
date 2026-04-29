using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.EscalateOrder;

public sealed class EscalateOrderCommandHandler : IRequestHandler<EscalateOrderCommand, Result>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EscalateOrderCommandHandler> _logger;

    public EscalateOrderCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<EscalateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(EscalateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure(OrderErrors.NotFound(command.OrderId));

        // Domain method enforces "only escalate Paid, non-already-escalated" — both no-ops.
        order.EscalateToAdmin(command.Reason?.Trim() ?? "Manual admin escalation");

        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Order {OrderNumber} escalated to admin. Reason: {Reason}",
            order.OrderNumber.Value,
            command.Reason);

        return Result.Success();
    }
}
