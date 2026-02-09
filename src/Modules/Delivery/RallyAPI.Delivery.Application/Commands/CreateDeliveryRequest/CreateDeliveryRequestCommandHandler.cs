using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Application.Services;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Errors;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.CreateDeliveryRequest;

public sealed class CreateDeliveryRequestCommandHandler
    : IRequestHandler<CreateDeliveryRequestCommand, Result<DeliveryRequestDto>>
{
    private readonly IDeliveryQuoteRepository _quoteRepository;
    private readonly IDeliveryRequestRepository _requestRepository;
    private readonly PrepTimeCalculator _prepTimeCalculator;
    private readonly ILogger<CreateDeliveryRequestCommandHandler> _logger;

    public CreateDeliveryRequestCommandHandler(
        IDeliveryQuoteRepository quoteRepository,
        IDeliveryRequestRepository requestRepository,
        PrepTimeCalculator prepTimeCalculator,
        ILogger<CreateDeliveryRequestCommandHandler> logger)
    {
        _quoteRepository = quoteRepository;
        _requestRepository = requestRepository;
        _prepTimeCalculator = prepTimeCalculator;
        _logger = logger;
    }

    public async Task<Result<DeliveryRequestDto>> Handle(
        CreateDeliveryRequestCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating delivery request for order {OrderNumber}",
            request.OrderNumber);

        // Get and validate quote
        var quote = await _quoteRepository.GetByIdAsync(request.QuoteId, cancellationToken);

        if (quote is null)
            return Result.Failure<DeliveryRequestDto>(DeliveryErrors.QuoteNotFound(request.QuoteId));

        if (quote.IsUsed)
            return Result.Failure<DeliveryRequestDto>(DeliveryErrors.QuoteAlreadyUsed(request.QuoteId));

        // Calculate dispatch time (early dispatch)
        var prepTime = _prepTimeCalculator.Calculate(request.ItemCount);
        var dispatchAt = DateTime.UtcNow.AddMinutes(prepTime.DispatchAfterMinutes);

        // Create delivery request
        var deliveryRequest = DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: request.OrderId,
            orderNumber: request.OrderNumber,
            quoteId: request.QuoteId,
            quotedPrice: quote.FinalFee,
            pickupLat: request.PickupLatitude,
            pickupLng: request.PickupLongitude,
            pickupPincode: request.PickupPincode,
            pickupAddress: request.PickupAddress,
            pickupContactName: request.PickupContactName,
            pickupContactPhone: request.PickupContactPhone,
            dropLat: request.DropLatitude,
            dropLng: request.DropLongitude,
            dropPincode: request.DropPincode,
            dropAddress: request.DropAddress,
            dropContactName: request.DropContactName,
            dropContactPhone: request.DropContactPhone,
            dispatchAt: dispatchAt,
            distanceKm: quote.DistanceKm,
            estimatedMinutes: quote.EstimatedMinutes);

        // Mark quote as used
        quote.MarkAsUsed(request.OrderId);
        await _quoteRepository.UpdateAsync(quote, cancellationToken);

        // Save delivery request
        await _requestRepository.AddAsync(deliveryRequest, cancellationToken);

        _logger.LogInformation(
            "Delivery request created: {DeliveryRequestId}, DispatchAt: {DispatchAt}",
            deliveryRequest.Id, dispatchAt);

        return Result.Success(MapToDto(deliveryRequest));
    }

    private static DeliveryRequestDto MapToDto(DeliveryRequest request)
    {
        return new DeliveryRequestDto
        {
            Id = request.Id,
            OrderId = request.OrderId,
            OrderNumber = request.OrderNumber,
            Status = request.Status.ToString(),
            FleetType = request.FleetType?.ToString(),
            QuotedPrice = request.QuotedPrice,
            CreatedAt = request.CreatedAt,
            DistanceKm = request.DistanceKm,
            EstimatedMinutes = request.EstimatedMinutes
        };
    }
}