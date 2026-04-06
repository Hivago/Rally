using MediatR;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.AddOrderNote;

internal sealed class AddOrderNoteCommandHandler
    : IRequestHandler<AddOrderNoteCommand, Result>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddOrderNoteCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        AddOrderNoteCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure(OrderErrors.NotFound(request.OrderId));

        // Append note with timestamp and admin ID
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var noteEntry = $"[{timestamp}] Admin {request.AdminId}: {request.Note.Trim()}";

        var existingNotes = order.InternalNotes;
        var updatedNotes = string.IsNullOrWhiteSpace(existingNotes)
            ? noteEntry
            : $"{existingNotes}\n{noteEntry}";

        order.UpdateInternalNotes(updatedNotes);

        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
