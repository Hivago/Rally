using MediatR;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Delivery.Infrastructure.Persistence;
using RallyAPI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace RallyAPI.Delivery.Application.Queries.GetTrackingInfo;

public sealed class GetTrackingInfoQueryHandler
    : IRequestHandler<GetTrackingInfoQuery, Result<TrackingDto>>
{
    private readonly DeliveryDbContext _dbContext;

    public GetTrackingInfoQueryHandler(DeliveryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TrackingDto>> Handle(
        GetTrackingInfoQuery request,
        CancellationToken cancellationToken)
    {
        var deliveryRequest = await _dbContext.DeliveryRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrderNumber == request.OrderNumber, cancellationToken);

        if (deliveryRequest is null)
        {
            return Result.Failure<TrackingDto>(
                Error.Validation($"No delivery found for order {request.OrderNumber}"));
        }

        return Result.Success(MapToTrackingDto(deliveryRequest));
    }

    private static TrackingDto MapToTrackingDto(DeliveryRequest request)
    {
        var isOwnFleet = request.FleetType == FleetType.OwnFleet;

        TrackingRiderDto? rider = null;
        if (request.RiderId.HasValue || !string.IsNullOrEmpty(request.ExternalRiderName))
        {
            rider = new TrackingRiderDto
            {
                Name = isOwnFleet ? request.RiderName ?? "Rider" : "Delivery Partner",
                Phone = isOwnFleet ? request.RiderPhone ?? "" : "",
                IsOwnFleet = isOwnFleet
            };
        }

        return new TrackingDto
        {
            OrderNumber = request.OrderNumber,
            Status = request.Status.ToString(),
            StatusText = GetStatusText(request.Status),
            Rider = rider,
            Eta = GetEta(request),
            EtaMinutes = request.EstimatedMinutes,
            Timeline = BuildTimeline(request)
        };
    }

    private static string GetStatusText(DeliveryRequestStatus status) => status switch
    {
        DeliveryRequestStatus.Created => "Finding delivery partner",
        DeliveryRequestStatus.PendingDispatch => "Preparing your order",
        DeliveryRequestStatus.SearchingOwnFleet => "Finding nearby rider",
        DeliveryRequestStatus.Searching3PL => "Assigning delivery partner",
        DeliveryRequestStatus.RiderAssigned => "Rider assigned",
        DeliveryRequestStatus.Assigned3PL => "Delivery partner assigned",
        DeliveryRequestStatus.RiderEnRoutePickup => "Rider heading to restaurant",
        DeliveryRequestStatus.RiderArrivedPickup => "Rider at restaurant",
        DeliveryRequestStatus.PickedUp => "Order picked up",
        DeliveryRequestStatus.RiderEnRouteDrop => "On the way to you",
        DeliveryRequestStatus.RiderArrivedDrop => "Rider has arrived",
        DeliveryRequestStatus.WaitingForCustomer => "Waiting for you",
        DeliveryRequestStatus.Delivered => "Delivered",
        DeliveryRequestStatus.Cancelled => "Cancelled",
        DeliveryRequestStatus.Failed => "Delivery failed",
        _ => "Processing"
    };

    private static string? GetEta(DeliveryRequest request)
    {
        if (request.Status >= DeliveryRequestStatus.Delivered)
            return null;

        if (!request.EstimatedMinutes.HasValue)
            return null;

        var remainingMinutes = request.Status switch
        {
            DeliveryRequestStatus.PickedUp => request.EstimatedMinutes.Value / 2,
            DeliveryRequestStatus.RiderEnRouteDrop => request.EstimatedMinutes.Value / 3,
            DeliveryRequestStatus.RiderArrivedDrop => 1,
            _ => request.EstimatedMinutes.Value
        };

        return $"~{remainingMinutes} mins";
    }

    private static List<TrackingTimelineItem> BuildTimeline(DeliveryRequest request)
    {
        var currentStatus = request.Status;

        return new List<TrackingTimelineItem>
        {
            new()
            {
                Status = "placed",
                Label = "Order Placed",
                At = request.CreatedAt,
                IsDone = true,
                IsCurrent = currentStatus == DeliveryRequestStatus.Created
            },
            new()
            {
                Status = "confirmed",
                Label = "Restaurant Confirmed",
                At = request.CreatedAt, // Simplified
                IsDone = currentStatus >= DeliveryRequestStatus.PendingDispatch,
                IsCurrent = currentStatus == DeliveryRequestStatus.PendingDispatch
            },
            new()
            {
                Status = "preparing",
                Label = "Preparing",
                At = null,
                IsDone = currentStatus >= DeliveryRequestStatus.SearchingOwnFleet,
                IsCurrent = currentStatus == DeliveryRequestStatus.SearchingOwnFleet ||
                           currentStatus == DeliveryRequestStatus.Searching3PL
            },
            new()
            {
                Status = "picked_up",
                Label = "Picked Up",
                At = request.PickedUpAt,
                IsDone = currentStatus >= DeliveryRequestStatus.PickedUp,
                IsCurrent = currentStatus == DeliveryRequestStatus.PickedUp ||
                           currentStatus == DeliveryRequestStatus.RiderEnRouteDrop
            },
            new()
            {
                Status = "delivering",
                Label = "Out for Delivery",
                At = request.PickedUpAt,
                IsDone = currentStatus >= DeliveryRequestStatus.RiderArrivedDrop,
                IsCurrent = currentStatus == DeliveryRequestStatus.RiderArrivedDrop ||
                           currentStatus == DeliveryRequestStatus.WaitingForCustomer
            },
            new()
            {
                Status = "delivered",
                Label = "Delivered",
                At = request.DeliveredAt,
                IsDone = currentStatus == DeliveryRequestStatus.Delivered,
                IsCurrent = currentStatus == DeliveryRequestStatus.Delivered
            }
        };
    }
}