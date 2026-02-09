using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Errors;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.MarkDelivered;

public sealed class MarkDeliveredCommandHandler : IRequestHandler<MarkDeliveredCommand, Result>
{
    private readonly IDeliveryRequestRepository _requestRepository;
    private readonly IRiderCommandService _riderCommandService;
    private readonly ILogger<MarkDeliveredCommandHandler> _logger;

    public MarkDeliveredCommandHandler(
        IDeliveryRequestRepository requestRepository,
        IRiderCommandService riderCommandService,
        ILogger<MarkDeliveredCommandHandler> logger)
    {
        _requestRepository = requestRepository;
        _riderCommandService = riderCommandService;
        _logger = logger;
    }

    public async Task<Result> Handle(MarkDeliveredCommand request, CancellationToken cancellationToken)
    {
        var deliveryRequest = await _requestRepository.GetByIdAsync(
            request.DeliveryRequestId, cancellationToken);

        if (deliveryRequest is null)
            return Result.Failure(DeliveryErrors.DeliveryRequestNotFound(request.DeliveryRequestId));

        if (deliveryRequest.RiderId != request.RiderId)
            return Result.Failure(Error.Validation("You are not assigned to this delivery."));

        try
        {
            deliveryRequest.MarkDelivered();
            await _requestRepository.UpdateAsync(deliveryRequest, cancellationToken);

            // Clear rider's current delivery
            await _riderCommandService.ClearRiderDeliveryAsync(
                request.RiderId,
                request.DeliveryRequestId,
                cancellationToken);

            _logger.LogInformation(
                "Delivery {DeliveryId} marked as delivered by rider {RiderId}",
                request.DeliveryRequestId, request.RiderId);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }
    }
}