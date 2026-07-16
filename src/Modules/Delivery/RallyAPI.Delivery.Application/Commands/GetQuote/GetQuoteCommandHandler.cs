using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.Abstractions.Distance;
using RallyAPI.SharedKernel.Abstractions.Geocoding;
using RallyAPI.SharedKernel.Abstractions.Pricing;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Utilities;

namespace RallyAPI.Delivery.Application.Commands.GetQuote;

public sealed class GetQuoteCommandHandler : IRequestHandler<GetQuoteCommand, Result<DeliveryQuoteDto>>
{
    private readonly IRiderQueryService _riderQueryService;
    private readonly IDeliveryPricingCalculator _pricingCalculator;
    private readonly IThirdPartyDeliveryProvider _thirdPartyProvider;
    private readonly IDeliveryQuoteRepository _quoteRepository;
    private readonly IGeocodingService _geocodingService;
    private readonly IDistanceCalculator _distanceCalculator;
    private readonly QuotePricingOptions _quotePricing;
    private readonly ILogger<GetQuoteCommandHandler> _logger;

    private const double SearchRadiusKm = 5.0;
    private const double MaxDeliveryDistanceKm = 5.0;

    public GetQuoteCommandHandler(
        IRiderQueryService riderQueryService,
        IDeliveryPricingCalculator pricingCalculator,
        IThirdPartyDeliveryProvider thirdPartyProvider,
        IDeliveryQuoteRepository quoteRepository,
        IGeocodingService geocodingService,
        IDistanceCalculator distanceCalculator,
        IOptions<QuotePricingOptions> quotePricing,
        ILogger<GetQuoteCommandHandler> logger)
    {
        _riderQueryService = riderQueryService;
        _pricingCalculator = pricingCalculator;
        _thirdPartyProvider = thirdPartyProvider;
        _quoteRepository = quoteRepository;
        _geocodingService = geocodingService;
        _distanceCalculator = distanceCalculator;
        _quotePricing = quotePricing.Value;
        _logger = logger;
    }

    /// <summary>
    /// Platform fee + GST on (delivery fee + platform fee). Applied uniformly to every quote —
    /// own fleet AND 3PL — because the customer always pays OUR price. Returns the two amounts and
    /// the breakdown (base delivery lines + Platform Fee + GST) serialized to JSON for storage.
    /// </summary>
    private (decimal PlatformFee, decimal GstAmount, string? BreakdownJson) BuildCustomerCharges(
        decimal deliveryFee,
        IReadOnlyList<PriceComponent>? deliveryBreakdown)
    {
        var platformFee = _quotePricing.CustomerPlatformFee;

        // GST is charged per component so delivery and platform rates can differ in future.
        var deliveryGst = Math.Round(deliveryFee * _quotePricing.DeliveryGstPercent / 100m, 2);
        var platformGst = Math.Round(platformFee * _quotePricing.PlatformGstPercent / 100m, 2);
        var gstAmount = deliveryGst + platformGst;

        var lines = new List<PriceComponent>();
        if (deliveryBreakdown is { Count: > 0 })
            lines.AddRange(deliveryBreakdown);
        else
            lines.Add(new PriceComponent("Delivery Fee", "Delivery charge", deliveryFee));

        if (platformFee > 0)
            lines.Add(new PriceComponent("Platform Fee", "Platform service fee", platformFee));
        if (gstAmount > 0)
            lines.Add(new PriceComponent("GST", "GST on delivery + platform fee", gstAmount));

        return (platformFee, gstAmount, JsonSerializer.Serialize(lines));
    }

    public async Task<Result<DeliveryQuoteDto>> Handle(
        GetQuoteCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting quote for restaurant {RestaurantId}, City: {City}, Fulfillment: {Fulfillment}",
            request.RestaurantId, request.City, request.IsPickup ? "Pickup" : "Delivery");

        // Pickup: no delivery leg. Skip distance/rider/3PL entirely and charge only the platform
        // fee + its GST on top of the item total — exactly what PlaceOrder bills a pickup order
        // (both read the same "Delivery:Quote" config, so quote and order stay in lockstep).
        // Nothing is persisted: PlaceOrder recomputes pickup pricing from config, not from a quote.
        if (request.IsPickup)
        {
            _logger.LogDebug("Pickup quote — pricing from config, no delivery leg");
            return Result.Success(BuildPickupDto(request.OrderAmount));
        }

        // Fail fast when the drop is outside our service area. Without this guard,
        // a 12km drop hits ProRouting and waits up to 30s for a guaranteed rejection.
        var distanceResult = await _distanceCalculator.GetDistanceAsync(
            request.PickupLatitude, request.PickupLongitude,
            request.DropLatitude, request.DropLongitude,
            cancellationToken);

        var distanceKm = distanceResult.IsSuccess
            ? (double)distanceResult.DistanceKm
            : GeoCalculator.CalculateDistanceKm(
                request.PickupLatitude, request.PickupLongitude,
                request.DropLatitude, request.DropLongitude);

        if (distanceKm > MaxDeliveryDistanceKm)
        {
            _logger.LogInformation(
                "Quote rejected: drop is {Distance:F1}km from restaurant (max {Max}km)",
                distanceKm, MaxDeliveryDistanceKm);
            return Result.Failure<DeliveryQuoteDto>(
                Error.Validation(
                    $"Delivery is only available within {MaxDeliveryDistanceKm:F0} km of the restaurant. This address is {distanceKm:F1} km away."));
        }

        // Resolve missing pincode/city via reverse geocoding so the UI doesn't have to.
        var (pickupPincode, dropPincode, city) = await ResolveLocationFieldsAsync(request, cancellationToken);

        // Check if own fleet is available
        var ownFleetAvailable = await _riderQueryService.IsOwnFleetAvailableAsync(
            request.PickupLatitude,
            request.PickupLongitude,
            SearchRadiusKm,
            cancellationToken);

        DeliveryQuote? quote = null;

        if (ownFleetAvailable)
        {
            _logger.LogDebug("Own fleet available, calculating own fleet price");
            quote = await CreateOwnFleetQuote(request, pickupPincode, dropPincode, city, cancellationToken);

            if (quote is null)
            {
                _logger.LogWarning("Own fleet price calculation failed, falling back to 3PL");
            }
        }

        if (quote is null)
        {
            _logger.LogDebug("Getting 3PL quote");
            quote = await CreateThirdPartyQuote(request, pickupPincode, dropPincode, city, cancellationToken);

            if (quote is null)
            {
                return Result.Failure<DeliveryQuoteDto>(
                    Error.Validation("No delivery options available for this location."));
            }
        }

        // Save quote
        await _quoteRepository.AddAsync(quote, cancellationToken);

        _logger.LogInformation(
            "Quote created: {QuoteId}, Fleet: {FleetType}, Fee: {Fee}",
            quote.Id, quote.FleetType, quote.FinalFee);

        return Result.Success(MapToDto(quote));
    }

    private async Task<(string PickupPincode, string DropPincode, string City)> ResolveLocationFieldsAsync(
        GetQuoteCommand request,
        CancellationToken ct)
    {
        var pickupPincode = request.PickupPincode;
        var dropPincode = request.DropPincode;
        var city = request.City;

        // Reverse geocode pickup if pickup pincode or city missing
        if (string.IsNullOrWhiteSpace(pickupPincode) || string.IsNullOrWhiteSpace(city))
        {
            var pickup = await _geocodingService.ReverseGeocodeAsync(
                request.PickupLatitude, request.PickupLongitude, ct);

            if (pickup.IsSuccess)
            {
                pickupPincode ??= pickup.Pincode;
                city ??= pickup.Locality;
            }
            else
            {
                _logger.LogWarning(
                    "Pickup reverse-geocode failed at ({Lat},{Lng}): {Error}",
                    request.PickupLatitude, request.PickupLongitude, pickup.Error);
            }
        }

        // Reverse geocode drop if drop pincode missing
        if (string.IsNullOrWhiteSpace(dropPincode))
        {
            var drop = await _geocodingService.ReverseGeocodeAsync(
                request.DropLatitude, request.DropLongitude, ct);

            if (drop.IsSuccess)
            {
                dropPincode ??= drop.Pincode;
                city ??= drop.Locality;
            }
            else
            {
                _logger.LogWarning(
                    "Drop reverse-geocode failed at ({Lat},{Lng}): {Error}",
                    request.DropLatitude, request.DropLongitude, drop.Error);
            }
        }

        return (pickupPincode ?? string.Empty, dropPincode ?? string.Empty, city ?? string.Empty);
    }

    private async Task<DeliveryQuote?> CreateOwnFleetQuote(
        GetQuoteCommand request,
        string pickupPincode,
        string dropPincode,
        string city,
        CancellationToken ct)
    {
        var priceResult = await _pricingCalculator.CalculateAsync(
            new DeliveryPriceRequest
            {
                PickupLatitude = request.PickupLatitude,
                PickupLongitude = request.PickupLongitude,
                DropLatitude = request.DropLatitude,
                DropLongitude = request.DropLongitude,
                City = city,
                OrderAmount = request.OrderAmount,
                RestaurantId = request.RestaurantId
            }, ct);

        if (!priceResult.IsSuccess)
        {
            _logger.LogWarning(
                "Own-fleet pricing failed: {Error}",
                priceResult.ErrorMessage);
            return null;
        }

        var (platformFee, gstAmount, breakdownJson) =
            BuildCustomerCharges(priceResult.FinalFee, priceResult.Breakdown);

        return DeliveryQuote.CreateOwnFleet(
            id: Guid.NewGuid(),
            pickupLat: request.PickupLatitude,
            pickupLng: request.PickupLongitude,
            pickupPincode: pickupPincode,
            dropLat: request.DropLatitude,
            dropLng: request.DropLongitude,
            dropPincode: dropPincode,
            city: city,
            orderAmount: request.OrderAmount,
            restaurantId: request.RestaurantId,
            distanceKm: priceResult.DistanceKm,
            baseFee: priceResult.BaseFee,
            finalFee: priceResult.FinalFee,
            estimatedMinutes: priceResult.EstimatedMinutes,
            expiresAt: priceResult.ExpiresAt,
            breakdownJson: breakdownJson,
            surgeMultiplier: priceResult.SurgeMultiplier,
            surgeReason: priceResult.SurgeReason,
            platformFee: platformFee,
            gstAmount: gstAmount);
    }

    private async Task<DeliveryQuote?> CreateThirdPartyQuote(
        GetQuoteCommand request,
        string pickupPincode,
        string dropPincode,
        string city,
        CancellationToken ct)
    {
        var quotesResult = await _thirdPartyProvider.GetQuotesAsync(
            new DeliveryQuoteRequest
            {
                PickupLatitude = request.PickupLatitude,
                PickupLongitude = request.PickupLongitude,
                PickupPincode = pickupPincode,
                DropLatitude = request.DropLatitude,
                DropLongitude = request.DropLongitude,
                DropPincode = dropPincode,
                City = city,
                OrderAmount = request.OrderAmount
            }, ct);

        if (!quotesResult.IsSuccess || !quotesResult.Quotes.Any())
        {
            _logger.LogWarning("No 3PL quotes available: {Error}", quotesResult.ErrorMessage);
            return null;
        }

        // Serviceability confirmed. We IGNORE the provider's price — the customer always pays OUR
        // tier price (the provider quote is our cost only). Compute our delivery fee + platform + GST.
        var bestQuote = quotesResult.Quotes.First();

        var priceResult = await _pricingCalculator.CalculateAsync(
            new DeliveryPriceRequest
            {
                PickupLatitude = request.PickupLatitude,
                PickupLongitude = request.PickupLongitude,
                DropLatitude = request.DropLatitude,
                DropLongitude = request.DropLongitude,
                City = city,
                OrderAmount = request.OrderAmount,
                RestaurantId = request.RestaurantId
            }, ct);

        if (!priceResult.IsSuccess)
        {
            _logger.LogWarning("Own-tier pricing failed for 3PL quote: {Error}", priceResult.ErrorMessage);
            return null;
        }

        var (platformFee, gstAmount, breakdownJson) =
            BuildCustomerCharges(priceResult.FinalFee, priceResult.Breakdown);

        return DeliveryQuote.CreateThirdParty(
            id: Guid.NewGuid(),
            pickupLat: request.PickupLatitude,
            pickupLng: request.PickupLongitude,
            pickupPincode: pickupPincode,
            dropLat: request.DropLatitude,
            dropLng: request.DropLongitude,
            dropPincode: dropPincode,
            city: city,
            orderAmount: request.OrderAmount,
            restaurantId: request.RestaurantId,
            price: priceResult.FinalFee,          // OUR delivery fee, not bestQuote.PriceForward
            estimatedMinutes: bestQuote.SlaMins,  // provider SLA for the ETA is fine
            providerName: quotesResult.ProviderName!,
            providerQuoteId: quotesResult.QuoteId!,
            expiresAt: priceResult.ExpiresAt,
            platformFee: platformFee,
            gstAmount: gstAmount,
            distanceKm: priceResult.DistanceKm,
            breakdownJson: breakdownJson);
    }

    /// <summary>
    /// Builds a pickup quote entirely from config: no delivery fee, no distance/ETA, just the flat
    /// platform fee + its GST charged on top of the item total. Not persisted.
    /// </summary>
    private DeliveryQuoteDto BuildPickupDto(decimal itemTotal)
    {
        var platformFee = _quotePricing.CustomerPlatformFee;
        var gst = Math.Round(platformFee * _quotePricing.PlatformGstPercent / 100m, 2);
        var foodGst = Math.Round(itemTotal * _quotePricing.FoodGstPercent / 100m, 2);
        var totalPayable = platformFee + gst + foodGst;

        var breakdown = new List<PriceBreakdownItem>();
        if (platformFee > 0)
            breakdown.Add(new PriceBreakdownItem("Platform Fee", "Platform service fee", platformFee));
        if (gst > 0)
            breakdown.Add(new PriceBreakdownItem("GST", "GST on platform fee", gst));
        if (foodGst > 0)
            breakdown.Add(new PriceBreakdownItem("GST on Food", $"{_quotePricing.FoodGstPercent}% GST on food", foodGst));

        return new DeliveryQuoteDto
        {
            Id = Guid.Empty, // no stored quote — pickup pricing is recomputed from config at order time
            FulfillmentType = "Pickup",
            ItemTotal = itemTotal,
            DeliveryFee = 0m,
            PlatformFee = platformFee,
            Gst = gst,
            FoodGst = foodGst,
            TotalPayable = totalPayable,
            GrandTotal = itemTotal + totalPayable,
            DistanceKm = 0m,
            EstimatedMinutes = 0,
            SurgeMultiplier = 1.0m,
            SurgeReason = null,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Breakdown = breakdown
        };
    }

    private DeliveryQuoteDto MapToDto(DeliveryQuote quote)
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

        // Food GST (5%) on the item total — the stored quote only holds delivery + platform + their
        // 18% GST (quote.CustomerTotal). Food GST is derived from OrderAmount here so the quote total
        // matches what PlaceOrder bills (OrderPricing.Tax uses the same rate).
        var foodGst = Math.Round(quote.OrderAmount * _quotePricing.FoodGstPercent / 100m, 2);
        if (foodGst > 0)
            breakdown.Add(new PriceBreakdownItem("GST on Food", $"{_quotePricing.FoodGstPercent}% GST on food", foodGst));

        var totalPayable = quote.CustomerTotal + foodGst;

        return new DeliveryQuoteDto
        {
            Id = quote.Id,
            FulfillmentType = "Delivery",
            ItemTotal = quote.OrderAmount,
            DeliveryFee = quote.FinalFee,
            PlatformFee = quote.PlatformFee,
            Gst = quote.GstAmount,
            FoodGst = foodGst,
            TotalPayable = totalPayable,
            GrandTotal = quote.OrderAmount + totalPayable,
            DistanceKm = quote.DistanceKm,
            EstimatedMinutes = quote.EstimatedMinutes,
            SurgeMultiplier = quote.SurgeMultiplier,
            SurgeReason = quote.SurgeReason,
            ExpiresAt = quote.ExpiresAt,
            Breakdown = breakdown
        };
    }
}
