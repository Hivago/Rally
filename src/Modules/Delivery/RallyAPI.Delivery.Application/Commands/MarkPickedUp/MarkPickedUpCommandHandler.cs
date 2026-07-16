using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.MarkPickedUp;

public sealed class MarkPickedUpCommandHandler : IRequestHandler<MarkPickedUpCommand, Result>
{
    private readonly IDeliveryRequestRepository _requestRepository;
    private readonly ILogger<MarkPickedUpCommandHandler> _logger;

    public MarkPickedUpCommandHandler(
        IDeliveryRequestRepository requestRepository,
        ILogger<MarkPickedUpCommandHandler> logger)
    {
        _requestRepository = requestRepository;
        _logger = logger;
    }

    public async Task<Result> Handle(MarkPickedUpCommand request, CancellationToken cancellationToken)
    {
        var deliveryRequest = await _requestRepository.GetByIdAsync(
            request.DeliveryRequestId, cancellationToken);

        if (deliveryRequest is null)
            return Result.Failure(DeliveryErrors.DeliveryRequestNotFound(request.DeliveryRequestId));

        if (deliveryRequest.RiderId != request.RiderId)
            return Result.Failure(Error.Validation("You are not assigned to this delivery."));

        if (!CodeMatches(deliveryRequest.PickupCode, request.PickupCode))
        {
            _logger.LogWarning(
                "Rider {RiderId} submitted an invalid pickup code for delivery {DeliveryId}",
                request.RiderId, request.DeliveryRequestId);
            return Result.Failure(DeliveryErrors.InvalidPickupCode);
        }

        try
        {
            deliveryRequest.MarkPickedUp();
            await _requestRepository.UpdateAsync(deliveryRequest, cancellationToken);

            _logger.LogInformation(
                "Delivery {DeliveryId} marked as picked up by rider {RiderId}",
                request.DeliveryRequestId, request.RiderId);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }
    }

    private static bool CodeMatches(string? expected, string? submitted) =>
        !string.IsNullOrWhiteSpace(expected) &&
        !string.IsNullOrWhiteSpace(submitted) &&
        string.Equals(expected.Trim(), submitted.Trim(), StringComparison.Ordinal);
}