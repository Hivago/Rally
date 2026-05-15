using RallyAPI.SharedKernel.Domain;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Domain.Entities;

/// <summary>
/// One-off scheduled closure window for a restaurant (holiday, training, kitchen pause, etc.).
/// Overrides <c>IsAcceptingOrders</c> during the window — order placement is blocked even if
/// the outlet is marked online. Cancelled closures are soft-deleted via <see cref="CancelledAt"/>
/// rather than removed, so history is preserved for audit.
/// </summary>
public sealed class RestaurantTimeOff : BaseEntity
{
    public Guid RestaurantId { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public string? Reason { get; private set; }
    public Guid CreatedByOwnerId { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    private const int MaxReasonLength = 200;
    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(90);

    private RestaurantTimeOff() { }

    private RestaurantTimeOff(
        Guid restaurantId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string? reason,
        Guid createdByOwnerId)
    {
        RestaurantId = restaurantId;
        StartsAt = startsAtUtc;
        EndsAt = endsAtUtc;
        Reason = reason;
        CreatedByOwnerId = createdByOwnerId;
    }

    public static Result<RestaurantTimeOff> Create(
        Guid restaurantId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string? reason,
        Guid createdByOwnerId,
        DateTime nowUtc)
    {
        if (restaurantId == Guid.Empty)
            return Result.Failure<RestaurantTimeOff>(Error.Validation("Restaurant ID is required."));

        if (createdByOwnerId == Guid.Empty)
            return Result.Failure<RestaurantTimeOff>(Error.Validation("Owner ID is required."));

        if (startsAtUtc >= endsAtUtc)
            return Result.Failure<RestaurantTimeOff>(
                Error.Validation("Start time must be earlier than end time."));

        if (endsAtUtc <= nowUtc)
            return Result.Failure<RestaurantTimeOff>(
                Error.Validation("End time must be in the future."));

        if (endsAtUtc - startsAtUtc > MaxDuration)
            return Result.Failure<RestaurantTimeOff>(
                Error.Validation($"Time off cannot exceed {MaxDuration.TotalDays:0} days."));

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmedReason is { Length: > MaxReasonLength })
            return Result.Failure<RestaurantTimeOff>(
                Error.Validation($"Reason must be {MaxReasonLength} characters or fewer."));

        return new RestaurantTimeOff(restaurantId, startsAtUtc, endsAtUtc, trimmedReason, createdByOwnerId);
    }

    public Result Cancel(DateTime nowUtc)
    {
        if (CancelledAt is not null)
            return Result.Failure(Error.Validation("Time off is already cancelled."));

        if (EndsAt <= nowUtc)
            return Result.Failure(Error.Validation("Cannot cancel a time off that has already ended."));

        CancelledAt = nowUtc;
        MarkAsUpdated();
        return Result.Success();
    }

    public bool IsActiveAt(DateTime momentUtc)
        => CancelledAt is null && momentUtc >= StartsAt && momentUtc < EndsAt;
}
