using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Delivery.Application.Services;

namespace RallyAPI.Delivery.Application.Commands.TriggerDispatch;

public class TriggerDispatchCommandHandler : IRequestHandler<TriggerDispatchCommand, Result>
{
    private readonly IDeliveryRequestRepository _deliveryRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RiderDispatchOrchestrator _riderDispatchOrchestrator;
    private readonly ILogger<TriggerDispatchCommandHandler> _logger;

    public TriggerDispatchCommandHandler(
        IDeliveryRequestRepository deliveryRequestRepository,
        IUnitOfWork unitOfWork,
        RiderDispatchOrchestrator riderDispatchOrchestrator,
        ILogger<TriggerDispatchCommandHandler> logger)
    {
        _deliveryRequestRepository = deliveryRequestRepository;
        _unitOfWork = unitOfWork;
        _riderDispatchOrchestrator = riderDispatchOrchestrator;
        _logger = logger;
    }

    public async Task<Result> Handle(
        TriggerDispatchCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "🚀 Triggering dispatch for DeliveryRequest {DeliveryRequestId}",
            request.DeliveryRequestId);

        var deliveryRequest = await _deliveryRequestRepository
            .GetByIdAsync(request.DeliveryRequestId, cancellationToken);

        if (deliveryRequest is null)
        {
            _logger.LogError("❌ DeliveryRequest not found");
            return Result.Failure(Error.NotFound("DeliveryRequest.NotFound"));
        }

        // Only start searching if it is newly created or pending a scheduled dispatch
        if (deliveryRequest.ShouldTriggerImmediateDispatch())
        {
            deliveryRequest.StartSearching();
            await _deliveryRequestRepository.UpdateAsync(deliveryRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "✅ Dispatch triggered for DeliveryRequest {DeliveryRequestId}",
            request.DeliveryRequestId);

        // Execute actual rider dispatch orchestrator here
        await _riderDispatchOrchestrator.DispatchAsync(deliveryRequest, cancellationToken);

        return Result.Success();
    }
}