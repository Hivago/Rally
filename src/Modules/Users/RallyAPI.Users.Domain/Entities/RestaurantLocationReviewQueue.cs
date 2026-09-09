using RallyAPI.SharedKernel.Domain;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Domain.Entities;

/// <summary>
/// One drift-detection finding for a restaurant: the pin at detection time, the
/// suggested corrected pin (centroid of the last N own-fleet arrival coordinates),
/// and its admin review state. Created by the Delivery-module drift sweep via
/// integration event, actioned by an admin in the review queue UI.
/// </summary>
public sealed class RestaurantLocationReviewQueue : BaseEntity
{
    public Guid RestaurantId { get; private set; }

    public decimal CurrentLatitude { get; private set; }
    public decimal CurrentLongitude { get; private set; }

    public decimal SuggestedLatitude { get; private set; }
    public decimal SuggestedLongitude { get; private set; }

    public decimal AverageDriftMeters { get; private set; }
    public int SampleSize { get; private set; }

    public RestaurantLocationReviewStatus Status { get; private set; }
    public DateTimeOffset DetectedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ReviewedByAdminId { get; private set; }

    private RestaurantLocationReviewQueue() { }

    private RestaurantLocationReviewQueue(
        Guid restaurantId,
        decimal currentLatitude,
        decimal currentLongitude,
        decimal suggestedLatitude,
        decimal suggestedLongitude,
        decimal averageDriftMeters,
        int sampleSize,
        DateTimeOffset detectedAt)
    {
        RestaurantId = restaurantId;
        CurrentLatitude = currentLatitude;
        CurrentLongitude = currentLongitude;
        SuggestedLatitude = suggestedLatitude;
        SuggestedLongitude = suggestedLongitude;
        AverageDriftMeters = averageDriftMeters;
        SampleSize = sampleSize;
        Status = RestaurantLocationReviewStatus.Pending;
        DetectedAt = detectedAt;
    }

    public static Result<RestaurantLocationReviewQueue> Create(
        Guid restaurantId,
        decimal currentLatitude,
        decimal currentLongitude,
        decimal suggestedLatitude,
        decimal suggestedLongitude,
        decimal averageDriftMeters,
        int sampleSize,
        DateTimeOffset detectedAt)
    {
        if (restaurantId == Guid.Empty)
            return Result.Failure<RestaurantLocationReviewQueue>(Error.Validation("Restaurant ID is required."));

        if (sampleSize <= 0)
            return Result.Failure<RestaurantLocationReviewQueue>(Error.Validation("Sample size must be positive."));

        if (averageDriftMeters <= 0)
            return Result.Failure<RestaurantLocationReviewQueue>(Error.Validation("Average drift must be positive."));

        return new RestaurantLocationReviewQueue(
            restaurantId, currentLatitude, currentLongitude,
            suggestedLatitude, suggestedLongitude,
            averageDriftMeters, sampleSize, detectedAt);
    }

    public Result Approve(Guid adminId, DateTimeOffset reviewedAt)
    {
        if (Status != RestaurantLocationReviewStatus.Pending)
            return Result.Failure(Error.Validation("This finding has already been reviewed."));

        Status = RestaurantLocationReviewStatus.Approved;
        ReviewedByAdminId = adminId;
        ReviewedAt = reviewedAt;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result Reject(Guid adminId, DateTimeOffset reviewedAt)
    {
        if (Status != RestaurantLocationReviewStatus.Pending)
            return Result.Failure(Error.Validation("This finding has already been reviewed."));

        Status = RestaurantLocationReviewStatus.Rejected;
        ReviewedByAdminId = adminId;
        ReviewedAt = reviewedAt;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result MarkAutoApplied(DateTimeOffset appliedAt)
    {
        if (Status != RestaurantLocationReviewStatus.Pending)
            return Result.Failure(Error.Validation("This finding has already been reviewed."));

        Status = RestaurantLocationReviewStatus.AutoApplied;
        ReviewedAt = appliedAt;
        MarkAsUpdated();
        return Result.Success();
    }
}
