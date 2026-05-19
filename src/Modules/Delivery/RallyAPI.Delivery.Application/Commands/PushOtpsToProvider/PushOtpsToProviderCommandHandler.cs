using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.PushOtpsToProvider;

internal sealed class PushOtpsToProviderCommandHandler
    : IRequestHandler<PushOtpsToProviderCommand, Result<PushOtpsToProviderResult>>
{
    private readonly IDeliveryRequestRepository _repository;
    private readonly IThirdPartyDeliveryProvider _provider;
    private readonly ILogger<PushOtpsToProviderCommandHandler> _logger;

    public PushOtpsToProviderCommandHandler(
        IDeliveryRequestRepository repository,
        IThirdPartyDeliveryProvider provider,
        ILogger<PushOtpsToProviderCommandHandler> logger)
    {
        _repository = repository;
        _provider = provider;
        _logger = logger;
    }

    public async Task<Result<PushOtpsToProviderResult>> Handle(
        PushOtpsToProviderCommand request,
        CancellationToken cancellationToken)
    {
        var delivery = await _repository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (delivery is null)
            return Result.Failure<PushOtpsToProviderResult>(
                Error.NotFound("DeliveryRequest", request.OrderId));

        if (!request.IsAdmin && delivery.RestaurantId != request.CallerId)
        {
            _logger.LogWarning(
                "Restaurant {CallerId} attempted to push OTPs for order {OrderId} owned by {OwnerId}",
                request.CallerId, request.OrderId, delivery.RestaurantId);
            return Result.Failure<PushOtpsToProviderResult>(
                Error.Validation("You are not authorized to update this order."));
        }

        if (string.IsNullOrEmpty(delivery.ExternalTaskId))
            return Result.Failure<PushOtpsToProviderResult>(
                Error.Validation("No 3PL task is associated with this order."));

        if (string.IsNullOrEmpty(delivery.PickupCode))
            return Result.Failure<PushOtpsToProviderResult>(
                Error.Validation("PickupCode is missing on the delivery request."));

        var result = await _provider.UpdateOrderAsync(
            new UpdateOrderRequest
            {
                ExternalTaskId = delivery.ExternalTaskId,
                PickupCode = delivery.PickupCode,
                DropCode = delivery.DropCode,
                OrderReady = true
            }, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Push OTPs failed for delivery {DeliveryId}, task {TaskId}: {Error}",
                delivery.Id, delivery.ExternalTaskId, result.ErrorMessage);
            return Result.Failure<PushOtpsToProviderResult>(
                Error.Validation(result.ErrorMessage ?? "Provider update failed"));
        }

        _logger.LogInformation(
            "Pushed OTPs for delivery {DeliveryId}, task {TaskId}. Provider state: {State}",
            delivery.Id, delivery.ExternalTaskId, result.State);

        return Result.Success(new PushOtpsToProviderResult(
            delivery.Id,
            delivery.ExternalTaskId,
            delivery.PickupCode,
            delivery.DropCode,
            result.State ?? string.Empty));
    }
}
