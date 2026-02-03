// RallyAPI.Pricing.Endpoints/Endpoints/CalculateDeliveryFee.cs
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Pricing.Application.DTOs;
using RallyAPI.Pricing.Application.Queries.CalculateDeliveryFee;
using RallyAPI.SharedKernel.Abstractions.Distance;
using RallyAPI.SharedKernel.Abstractions.Pricing;

namespace RallyAPI.Pricing.Endpoints.Endpoints;

public class CalculateDeliveryFee : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/pricing/delivery-fee", HandleAsync)
            .WithTags("Pricing")
            .WithSummary("Calculate delivery fee with quote")
            .WithDescription("Returns delivery fee with quote ID valid for 10 minutes")
            .AllowAnonymous();

        app.MapGet("/api/pricing/distance", async (
    IDistanceCalculator distanceCalculator,
    IDeliveryPricingCalculator pricingCalculator) =>
        {
            // Test: Connaught Place → India Gate (Delhi)
            var distance = await distanceCalculator.GetDistanceAsync(
                28.6315, 77.2167,  // Connaught Place
                28.6129, 77.2295); // India Gate

            var pricing = await pricingCalculator.CalculateAsync(new DeliveryPriceRequest
            {
                PickupLatitude = 28.6315,
                PickupLongitude = 77.2167,
                DropLatitude = 28.6129,
                DropLongitude = 77.2295,
                City = "Delhi",
                OrderAmount = 500
            });

            return Results.Ok(new
            {
                Distance = new
                {
                    distance.DistanceKm,
                    distance.DurationMinutes,
                    distance.DistanceText,
                    distance.DurationText,
                    distance.IsSuccess,
                    distance.ErrorMessage
                },
                Pricing = new
                {
                    pricing.FinalFee,
                    pricing.BaseFee,
                    pricing.DistanceKm,
                    pricing.EstimatedMinutes,
                    pricing.QuoteId,
                    pricing.Breakdown,
                    pricing.IsSuccess,
                    pricing.ErrorMessage
                }
            });
        })
.WithTags("Test");
    }

    private static async Task<IResult> HandleAsync(
        CalculateDeliveryFeeRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var query = new CalculateDeliveryFeeQuery(
            request.RestaurantLatitude,
            request.RestaurantLongitude,
            request.RestaurantPincode,
            request.CustomerLatitude,
            request.CustomerLongitude,
            request.CustomerPincode,
            request.City,
            request.OrderSubtotal,
            request.OrderWeight,
            ItemCount: 1,
            request.RestaurantId,
            request.CustomerId,
            request.PromoCode);

        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }
}