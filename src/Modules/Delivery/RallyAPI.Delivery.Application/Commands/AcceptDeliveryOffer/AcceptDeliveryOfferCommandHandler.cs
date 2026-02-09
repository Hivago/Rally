using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Delivery.Domain.Errors;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.AcceptDeliveryOffer;

public sealed class AcceptDeliveryOfferCommandHandler
    : IRequestHandler<AcceptDeliveryOfferCommand, Result<DeliveryRequestDto>>
{
    private readonly IDeliveryRequestRepository _requestRepository;
    private readonly IRiderQueryService _riderQueryService;
    private readonly IRiderCommandService _riderCommandService;
    private readonly ILogger<AcceptDeliveryOfferCommandHandler> _logger;

    public AcceptDeliveryOfferCommandHandler(
        IDeliveryRequestRepository requestRepository,
        IRiderQueryService riderQueryService,
        IRiderCommandService riderCommandService,
        ILogger<AcceptDeliveryOfferCommandHandler> logger)
    {
        _requestRepository = requestRepository;
        _riderQueryService = riderQueryService;
        _riderCommandService = riderCommandService;
        _logger = logger;
    }

    public async Task<Result<DeliveryRequestDto>> Handle(
        AcceptDeliveryOfferCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Rider {RiderId} accepting offer {OfferId}",
            request.RiderId, request.OfferId);

        // Find the delivery request containing this offer
        // For simplicity, we'll query by offer - in production, consider a separate offers table query
        var deliveryRequests = await _requestRepository.GetByStatusAsync(
            DeliveryRequestStatus.SearchingOwnFleet, cancellationToken);

        DeliveryRequest? deliveryRequest = null;
        RiderOffer? offer = null;

        foreach (var dr in deliveryRequests)
        {
            var drWithOffers = await _requestRepository.GetByIdWithOffersAsync(dr.Id, cancellationToken);
            offer = drWithOffers?.RiderOffers.FirstOrDefault(o => o.Id == request.OfferId);
            if (offer != null)
            {
                deliveryRequest = drWithOffers;
                break;
            }
        }

        if (deliveryRequest is null || offer is null)
        {
            return Result.Failure<DeliveryRequestDto>(
                Error.Validation("Offer not found or delivery already assigned."));
        }

        // Validate offer
        if (offer.RiderId != request.RiderId)
        {
            return Result.Failure<DeliveryRequestDto>(
                Error.Validation("This offer was not sent to you."));
        }

        if (offer.IsExpired)
        {
            return Result.Failure<DeliveryRequestDto>(DeliveryErrors.OfferExpired);
        }

        if (offer.Status != RiderOfferStatus.Pending)
        {
            return Result.Failure<DeliveryRequestDto>(DeliveryErrors.OfferAlreadyResponded);
        }

        // Get rider details
        var rider = await _riderQueryService.GetRiderByIdAsync(request.RiderId, cancellationToken);
        if (rider is null)
        {
            return Result.Failure<DeliveryRequestDto>(Error.NotFound("Rider", request.RiderId));
        }

        // Accept offer
        offer.Accept();

        // Expire all other pending offers
        deliveryRequest.ExpireAllPendingOffers();

        // Assign rider to delivery
        deliveryRequest.AssignOwnFleetRider(
            rider.RiderId,
            rider.Name,
            rider.Phone);

        // Assign delivery to rider (in Users module)
        var assignResult = await _riderCommandService.AssignDeliveryToRiderAsync(
            request.RiderId,
            deliveryRequest.Id,
            cancellationToken);

        if (assignResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to assign delivery to rider: {Error}",
                assignResult.Error.Message);
            return Result.Failure<DeliveryRequestDto>(assignResult.Error);
        }

        // Save
        await _requestRepository.UpdateAsync(deliveryRequest, cancellationToken);

        _logger.LogInformation(
            "Rider {RiderId} assigned to delivery {DeliveryId}",
            request.RiderId, deliveryRequest.Id);

        return Result.Success(MapToDto(deliveryRequest, rider));
    }

    private static DeliveryRequestDto MapToDto(DeliveryRequest request, RiderDetails rider)
    {
        return new DeliveryRequestDto
        {
            Id = request.Id,
            OrderId = request.OrderId,
            OrderNumber = request.OrderNumber,
            Status = request.Status.ToString(),
            FleetType = request.FleetType?.ToString(),
            QuotedPrice = request.QuotedPrice,
            Rider = new RiderInfoDto
            {
                RiderId = rider.RiderId,
                Name = rider.Name,
                Phone = rider.Phone,
                IsOwnFleet = true
            },
            CreatedAt = request.CreatedAt,
            AssignedAt = request.AssignedAt,
            DistanceKm = request.DistanceKm,
            EstimatedMinutes = request.EstimatedMinutes
        };
    }
}