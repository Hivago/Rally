using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Errors;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.MarkFailed;

public sealed class MarkFailedCommandHandler : IRequestHandler<MarkFailedCommand, Result>
{
    private readonly IDeliveryRequestRepository _requestRepository;
    private readonly IRiderCommandService _riderCommandService;
    private readonly ILogger<MarkFailedCommandHandler> _logger;

    public MarkFailedCommandHandler(
        IDeliveryRequestRepository requestRepository,
        IRiderCommandService riderCommandService,
        ILogger<MarkFailedCommandHandler> logger)
    {
        _requestRepository = requestRepository;
        _riderCommandService = riderCommandService;
        _logger = logger;
    }

    public async Task<Result> Handle(MarkFailedCommand request, CancellationToken cancellationToken)
    {
        var deliveryRequest = await _requestRepository.GetByIdAsync(
            request.DeliveryRequestId, cancellationToken);

        if (deliveryRequest is null)
            return Result.Failure(DeliveryErrors.DeliveryRequestNotFound(request.DeliveryRequestId));

        if (deliveryRequest.RiderId != request.RiderId)
            return Result.Failure(Error.Validation("You are not assigned to this delivery."));

        deliveryRequest.MarkFailed(request.Reason, request.Notes, request.PhotoUrl);
        await _requestRepository.UpdateAsync(deliveryRequest, cancellationToken);

        // Clear rider's current delivery
        await _riderCommandService.ClearRiderDeliveryAsync(
            request.RiderId,
            request.DeliveryRequestId,
            cancellationToken);

        _logger.LogWarning(
            "Delivery {DeliveryId} marked as failed. Reason: {Reason}",
            request.DeliveryRequestId, request.Reason);

        return Result.Success();
    }
}