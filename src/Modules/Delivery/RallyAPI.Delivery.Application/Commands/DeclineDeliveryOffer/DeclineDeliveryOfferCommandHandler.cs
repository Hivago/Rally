using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Delivery.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.DeclineDeliveryOffer;

public sealed class DeclineDeliveryOfferCommandHandler : IRequestHandler<DeclineDeliveryOfferCommand, Result>
{
    private readonly IDeliveryRequestRepository _requestRepository;
    private readonly ILogger<DeclineDeliveryOfferCommandHandler> _logger;

    public DeclineDeliveryOfferCommandHandler(
        IDeliveryRequestRepository requestRepository,
        ILogger<DeclineDeliveryOfferCommandHandler> logger)
    {
        _requestRepository = requestRepository;
        _logger = logger;
    }

    public async Task<Result> Handle(DeclineDeliveryOfferCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Rider {RiderId} declining offer {OfferId}. Reason: {Reason}",
            request.RiderId, request.OfferId, request.Reason ?? "none");

        // Find the delivery request containing this offer
        var deliveryRequests = await _requestRepository.GetByStatusAsync(
            DeliveryRequestStatus.SearchingOwnFleet, cancellationToken);

        DeliveryRequest? deliveryRequest = null;
        RiderOffer? offer = null;

        foreach (var dr in deliveryRequests)
        {
            var drWithOffers = await _requestRepository.GetByIdWithOffersAsync(dr.Id, cancellationToken);
            offer = drWithOffers?.RiderOffers.FirstOrDefault(o => o.Id == request.OfferId);
            if (offer is not null)
            {
                deliveryRequest = drWithOffers;
                break;
            }
        }

        if (deliveryRequest is null || offer is null)
            return Result.Failure(Error.Validation("Offer not found or delivery already assigned."));

        if (offer.RiderId != request.RiderId)
            return Result.Failure(Error.Validation("This offer was not sent to you."));

        if (offer.Status != RiderOfferStatus.Pending)
            return Result.Failure(DeliveryErrors.OfferAlreadyResponded);

        if (offer.IsExpired)
            return Result.Failure(DeliveryErrors.OfferExpired);

        offer.Reject(request.Reason);

        // Concurrency-guarded: the dispatcher may write this same row (expire offers / fall to
        // 3PL) at the same moment. On a lost race the reject simply didn't stick — surface it as
        // a benign "already responded" rather than a 500.
        if (!await _requestRepository.TryUpdateAsync(deliveryRequest, cancellationToken))
        {
            _logger.LogInformation(
                "Decline of offer {OfferId} by rider {RiderId} lost a concurrency race; offer already resolved.",
                request.OfferId, request.RiderId);
            return Result.Failure(DeliveryErrors.OfferAlreadyResponded);
        }

        _logger.LogInformation(
            "Offer {OfferId} declined by rider {RiderId}. Dispatch orchestrator will fall back on timeout.",
            request.OfferId, request.RiderId);

        return Result.Success();
    }
}
