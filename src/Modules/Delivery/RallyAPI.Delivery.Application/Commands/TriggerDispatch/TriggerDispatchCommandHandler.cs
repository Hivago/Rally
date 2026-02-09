using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.TriggerDispatch;

public class TriggerDispatchCommandHandler : IRequestHandler<TriggerDispatchCommand, Result>
{
    private readonly IDeliveryRequestRepository _deliveryRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TriggerDispatchCommandHandler> _logger;

    public TriggerDispatchCommandHandler(
        IDeliveryRequestRepository deliveryRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<TriggerDispatchCommandHandler> logger)
    {
        _deliveryRequestRepository = deliveryRequestRepository;
        _unitOfWork = unitOfWork;
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

        // Start searching for riders
        deliveryRequest.StartSearching();

        _deliveryRequestRepository.UpdateAsync(deliveryRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "✅ Dispatch triggered for DeliveryRequest {DeliveryRequestId}",
            request.DeliveryRequestId);

        // TODO: Trigger actual rider dispatch orchestrator here
        // await _riderDispatchOrchestrator.DispatchAsync(deliveryRequest, cancellationToken);

        return Result.Success();
    }
}