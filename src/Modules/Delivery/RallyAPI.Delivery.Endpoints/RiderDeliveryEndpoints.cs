using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Delivery.Application.Commands.AcceptDeliveryOffer;
using RallyAPI.Delivery.Application.Commands.MarkDelivered;
using RallyAPI.Delivery.Application.Commands.MarkFailed;
using RallyAPI.Delivery.Application.Commands.MarkPickedUp;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Endpoints.Requests;
using RallyAPI.Delivery.Infrastructure.Persistence;
using RallyAPI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Orders.Application.Abstractions;

namespace RallyAPI.Delivery.Endpoints;

public static class RiderDeliveryEndpoints
{
    public static IEndpointRouteBuilder MapRiderDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rider/delivery")
            .WithTags("Rider Delivery")
            .RequireAuthorization("Rider")
            .WithOpenApi();

        // Get Pending Offer for Rider
        group.MapGet("/pending-offer", GetPendingOffer)
            .WithName("GetPendingOffer")
            .WithSummary("Get pending delivery offer for rider")
            .Produces<RiderOfferDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent);

        // Accept Offer
        group.MapPost("/offer/{offerId:guid}/accept", AcceptOffer)
            .WithName("AcceptDeliveryOffer")
            .WithSummary("Accept a delivery offer")
            .Produces<DeliveryRequestDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Reject Offer
        group.MapPost("/offer/{offerId:guid}/reject", RejectOffer)
            .WithName("RejectDeliveryOffer")
            .WithSummary("Reject a delivery offer")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Mark Arrived at Pickup
        group.MapPost("/{deliveryId:guid}/arrived-pickup", ArrivedAtPickup)
            .WithName("ArrivedAtPickup")
            .WithSummary("Mark rider arrived at restaurant")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Mark Picked Up
        group.MapPost("/{deliveryId:guid}/pickup", MarkPickedUp)
            .WithName("MarkDeliveryPickedUp")
            .WithSummary("Mark order as picked up")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Mark Arrived at Drop
        group.MapPost("/{deliveryId:guid}/arrived-drop", ArrivedAtDrop)
            .WithName("ArrivedAtDrop")
            .WithSummary("Mark rider arrived at customer")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Mark Delivered
        group.MapPost("/{deliveryId:guid}/delivered", MarkDelivered)
            .WithName("MarkDeliveryDelivered")
            .WithSummary("Mark order as delivered")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Mark Failed
        group.MapPost("/{deliveryId:guid}/failed", MarkFailed)
            .WithName("MarkDeliveryFailed")
            .WithSummary("Mark delivery as failed")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Get Current Delivery
        group.MapGet("/current", GetCurrentDelivery)
            .WithName("GetCurrentDelivery")
            .WithSummary("Get rider's current active delivery")
            .Produces<DeliveryRequestDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static async Task<IResult> GetPendingOffer(
        ICurrentUserService currentUser,
        DeliveryDbContext dbContext,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Results.Unauthorized();

        var riderId = currentUser.UserId.Value;

        var pendingOffer = await dbContext.RiderOffers
            .AsNoTracking()
            .Where(o => o.RiderId == riderId)
            .Where(o => o.Status == RiderOfferStatus.Pending)
            .Where(o => o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.OfferedAt)
            .FirstOrDefaultAsync(ct);

        if (pendingOffer is null)
            return Results.NoContent();

        var deliveryRequest = await dbContext.DeliveryRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == pendingOffer.DeliveryRequestId, ct);

        if (deliveryRequest is null)
            return Results.NoContent();

        var dto = new RiderOfferDto
        {
            OfferId = pendingOffer.Id,
            DeliveryRequestId = pendingOffer.DeliveryRequestId,
            OrderNumber = deliveryRequest.OrderNumber,
            RestaurantName = deliveryRequest.PickupContactName,
            PickupAddress = deliveryRequest.PickupAddress,
            PickupLatitude = deliveryRequest.PickupLatitude,
            PickupLongitude = deliveryRequest.PickupLongitude,
            DropAddress = deliveryRequest.DropAddress,
            DropLatitude = deliveryRequest.DropLatitude,
            DropLongitude = deliveryRequest.DropLongitude,
            DistanceToPickupKm = pendingOffer.DistanceToRestaurantKm ?? 0,
            DistanceToDropKm = deliveryRequest.DistanceKm ?? 0,
            TotalDistanceKm = (pendingOffer.DistanceToRestaurantKm ?? 0) + (deliveryRequest.DistanceKm ?? 0),
            Earnings = pendingOffer.Earnings,
            ExpiresInSeconds = Math.Max(0, (int)(pendingOffer.ExpiresAt - DateTime.UtcNow).TotalSeconds),
            ExpiresAt = pendingOffer.ExpiresAt,
            IsFoodReady = deliveryRequest.Status >= DeliveryRequestStatus.RiderArrivedPickup
        };

        return Results.Ok(dto);
    }

    private static async Task<IResult> AcceptOffer(
        Guid offerId,
        ICurrentUserService currentUser,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Results.Unauthorized();

        var command = new AcceptDeliveryOfferCommand
        {
            OfferId = offerId,
            RiderId = currentUser.UserId.Value
        };

        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> RejectOffer(
        Guid offerId,
        [FromBody] RejectOfferRequest? request,
        ICurrentUserService currentUser,
        DeliveryDbContext dbContext,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Results.Unauthorized();

        var offer = await dbContext.RiderOffers
            .FirstOrDefaultAsync(o => o.Id == offerId && o.RiderId == currentUser.UserId.Value, ct);

        if (offer is null)
            return Results.NotFound();

        if (offer.Status != RiderOfferStatus.Pending)
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid State",
                Detail = "This offer has already been responded to."
            });

        offer.Reject(request?.Reason);
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(new { message = "Offer rejected" });
    }

    private static async Task<IResult> ArrivedAtPickup(
        Guid deliveryId,
        ICurrentUserService currentUser,
        DeliveryDbContext dbContext,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Results.Unauthorized();

        var delivery = await dbContext.DeliveryRequests
            .FirstOrDefaultAsync(r => r.Id == deliveryId && r.RiderId == currentUser.UserId.Value, ct);

        if (delivery is null)
            return Results.NotFound();

        try
        {
            delivery.MarkRiderArrivedPickup();
            await dbContext.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Arrived at pickup" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid State",
                Detail = ex.Message
            });
        }
    }

    private static async Task<IResult> MarkPickedUp(
        Guid deliveryId,
        ICurrentUserService currentUser,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Results.Unauthorized();

        var command = new MarkPickedUpCommand
        {
            DeliveryRequestId = deliveryId,
            RiderId = currentUser.UserId.Value
        };

        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new { message = "Order picked up" })
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> ArrivedAtDrop(
        Guid deliveryId,
        ICurrentUserService currentUser,
        DeliveryDbContext dbContext,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Results.Unauthorized();

        var delivery = await dbContext.DeliveryRequests
            .FirstOrDefaultAsync(r => r.Id == deliveryId && r.RiderId == currentUser.UserId.Value, ct);

        if (delivery is null)
            return Results.NotFound();

        try
        {
            delivery.MarkRiderArrivedDrop();
            await dbContext.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Arrived at drop location" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid State",
                Detail = ex.Message
            });
        }
    }

    private static async Task<IResult> MarkDelivered(
        Guid deliveryId,
        ICurrentUserService currentUser,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Results.Unauthorized();

        var command = new MarkDeliveredCommand
        {
            DeliveryRequestId = deliveryId,
            RiderId = currentUser.UserId.Value
        };

        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new { message = "Delivery completed" })
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> MarkFailed(
        Guid deliveryId,
        [FromBody] MarkFailedRequest request,
        ICurrentUserService currentUser,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Results.Unauthorized();

        var command = new MarkFailedCommand
        {
            DeliveryRequestId = deliveryId,
            RiderId = currentUser.UserId.Value,
            Reason = request.Reason,
            Notes = request.Notes,
            PhotoUrl = request.PhotoUrl
        };

        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new { message = "Delivery marked as failed" })
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> GetCurrentDelivery(
        ICurrentUserService currentUser,
        DeliveryDbContext dbContext,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Results.Unauthorized();

        var riderId = currentUser.UserId.Value;

        var activeDelivery = await dbContext.DeliveryRequests
            .AsNoTracking()
            .Where(r => r.RiderId == riderId)
            .Where(r => r.Status >= DeliveryRequestStatus.RiderAssigned)
            .Where(r => r.Status < DeliveryRequestStatus.Delivered)
            .FirstOrDefaultAsync(ct);

        if (activeDelivery is null)
            return Results.NoContent();

        var dto = new DeliveryRequestDto
        {
            Id = activeDelivery.Id,
            OrderId = activeDelivery.OrderId,
            OrderNumber = activeDelivery.OrderNumber,
            Status = activeDelivery.Status.ToString(),
            FleetType = activeDelivery.FleetType?.ToString(),
            QuotedPrice = activeDelivery.QuotedPrice,
            Rider = new RiderInfoDto
            {
                RiderId = activeDelivery.RiderId,
                Name = activeDelivery.RiderName,
                Phone = activeDelivery.RiderPhone,
                IsOwnFleet = true
            },
            CreatedAt = activeDelivery.CreatedAt,
            AssignedAt = activeDelivery.AssignedAt,
            PickedUpAt = activeDelivery.PickedUpAt,
            DistanceKm = activeDelivery.DistanceKm,
            EstimatedMinutes = activeDelivery.EstimatedMinutes
        };

        return Results.Ok(dto);
    }

    private static ProblemDetails CreateProblemDetails(Error error) => new()
    {
        Title = error.Code,
        Detail = error.Message,
        Status = error.Code.Contains("NotFound") ? 404 : 400
    };
}

public sealed record RejectOfferRequest
{
    public string? Reason { get; init; }
}