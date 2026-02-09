using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.Abstractions.Pricing;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Commands.GetQuote;

public sealed class GetQuoteCommandHandler : IRequestHandler<GetQuoteCommand, Result<DeliveryQuoteDto>>
{
    private readonly IRiderQueryService _riderQueryService;
    private readonly IDeliveryPricingCalculator _pricingCalculator;
    private readonly IThirdPartyDeliveryProvider _thirdPartyProvider;
    private readonly IDeliveryQuoteRepository _quoteRepository;
    private readonly ILogger<GetQuoteCommandHandler> _logger;

    private const double SearchRadiusKm = 5.0;

    public GetQuoteCommandHandler(
        IRiderQueryService riderQueryService,
        IDeliveryPricingCalculator pricingCalculator,
        IThirdPartyDeliveryProvider thirdPartyProvider,
        IDeliveryQuoteRepository quoteRepository,
        ILogger<GetQuoteCommandHandler> logger)
    {
        _riderQueryService = riderQueryService;
        _pricingCalculator = pricingCalculator;
        _thirdPartyProvider = thirdPartyProvider;
        _quoteRepository = quoteRepository;
        _logger = logger;
    }

    public async Task<Result<DeliveryQuoteDto>> Handle(
        GetQuoteCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting delivery quote for restaurant {RestaurantId}, City: {City}",
            request.RestaurantId, request.City);

        // Check if own fleet is available
        var ownFleetAvailable = await _riderQueryService.IsOwnFleetAvailableAsync(
            request.PickupLatitude,
            request.PickupLongitude,
            SearchRadiusKm,
            cancellationToken);

        DeliveryQuote quote;

        if (ownFleetAvailable)
        {
            _logger.LogDebug("Own fleet available, calculating own fleet price");
            quote = await CreateOwnFleetQuote(request, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Own fleet not available, getting 3PL quote");
            var thirdPartyQuote = await CreateThirdPartyQuote(request, cancellationToken);

            if (thirdPartyQuote is null)
            {
                return Result.Failure<DeliveryQuoteDto>(
                    Error.Validation("No delivery options available for this location."));
            }

            quote = thirdPartyQuote;
        }

        // Save quote
        await _quoteRepository.AddAsync(quote, cancellationToken);

        _logger.LogInformation(
            "Quote created: {QuoteId}, Fleet: {FleetType}, Fee: {Fee}",
            quote.Id, quote.FleetType, quote.FinalFee);

        return Result.Success(MapToDto(quote));
    }

    private async Task<DeliveryQuote> CreateOwnFleetQuote(
        GetQuoteCommand request,
        CancellationToken ct)
    {
        var priceResult = await _pricingCalculator.CalculateAsync(
            new DeliveryPriceRequest
            {
                PickupLatitude = request.PickupLatitude,
                PickupLongitude = request.PickupLongitude,
                DropLatitude = request.DropLatitude,
                DropLongitude = request.DropLongitude,
                City = request.City,
                OrderAmount = request.OrderAmount,
                RestaurantId = request.RestaurantId
            }, ct);

        var breakdownJson = priceResult.Breakdown.Any()
            ? JsonSerializer.Serialize(priceResult.Breakdown)
            : null;

        return DeliveryQuote.CreateOwnFleet(
            id: Guid.NewGuid(),
            pickupLat: request.PickupLatitude,
            pickupLng: request.PickupLongitude,
            pickupPincode: request.PickupPincode,
            dropLat: request.DropLatitude,
            dropLng: request.DropLongitude,
            dropPincode: request.DropPincode,
            city: request.City,
            orderAmount: request.OrderAmount,
            restaurantId: request.RestaurantId,
            distanceKm: priceResult.DistanceKm,
            baseFee: priceResult.BaseFee,
            finalFee: priceResult.FinalFee,
            estimatedMinutes: priceResult.EstimatedMinutes,
            expiresAt: priceResult.ExpiresAt,
            breakdownJson: breakdownJson,
            surgeMultiplier: priceResult.SurgeMultiplier,
            surgeReason: priceResult.SurgeReason);
    }

    private async Task<DeliveryQuote?> CreateThirdPartyQuote(
        GetQuoteCommand request,
        CancellationToken ct)
    {
        var quotesResult = await _thirdPartyProvider.GetQuotesAsync(
            new DeliveryQuoteRequest
            {
                PickupLatitude = request.PickupLatitude,
                PickupLongitude = request.PickupLongitude,
                PickupPincode = request.PickupPincode,
                DropLatitude = request.DropLatitude,
                DropLongitude = request.DropLongitude,
                DropPincode = request.DropPincode,
                City = request.City,
                OrderAmount = request.OrderAmount
            }, ct);

        if (!quotesResult.IsSuccess || !quotesResult.Quotes.Any())
        {
            _logger.LogWarning("No 3PL quotes available: {Error}", quotesResult.ErrorMessage);
            return null;
        }

        // Select best quote (already sorted by price in provider)
        var bestQuote = quotesResult.Quotes.First();

        return DeliveryQuote.CreateThirdParty(
            id: Guid.NewGuid(),
            pickupLat: request.PickupLatitude,
            pickupLng: request.PickupLongitude,
            pickupPincode: request.PickupPincode,
            dropLat: request.DropLatitude,
            dropLng: request.DropLongitude,
            dropPincode: request.DropPincode,
            city: request.City,
            orderAmount: request.OrderAmount,
            restaurantId: request.RestaurantId,
            price: bestQuote.PriceForward,
            estimatedMinutes: bestQuote.SlaMins,
            providerName: quotesResult.ProviderName!,
            providerQuoteId: quotesResult.QuoteId!,
            expiresAt: quotesResult.ValidUntil ?? DateTime.UtcNow.AddMinutes(5));
    }

    private static DeliveryQuoteDto MapToDto(DeliveryQuote quote)
    {
        var breakdown = new List<PriceBreakdownItem>();

        if (!string.IsNullOrEmpty(quote.BreakdownJson))
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<PriceBreakdownItem>>(quote.BreakdownJson);
                if (items != null) breakdown = items;
            }
            catch { /* Ignore parsing errors */ }
        }

        return new DeliveryQuoteDto
        {
            Id = quote.Id,
            DeliveryFee = quote.FinalFee,
            DistanceKm = quote.DistanceKm,
            EstimatedMinutes = quote.EstimatedMinutes,
            SurgeMultiplier = quote.SurgeMultiplier,
            SurgeReason = quote.SurgeReason,
            ExpiresAt = quote.ExpiresAt,
            Breakdown = breakdown
        };
    }
}