using RallyAPI.SharedKernel.Domain;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Domain.Entities;

public sealed class RestaurantScheduleSlot : BaseEntity
{
    public Guid RestaurantId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly OpensAt { get; private set; }
    public TimeOnly ClosesAt { get; private set; }

    private RestaurantScheduleSlot() { }

    private RestaurantScheduleSlot(Guid restaurantId, DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        RestaurantId = restaurantId;
        DayOfWeek = dayOfWeek;
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    public static Result<RestaurantScheduleSlot> Create(
        Guid restaurantId,
        DayOfWeek dayOfWeek,
        TimeOnly opensAt,
        TimeOnly closesAt)
    {
        if (restaurantId == Guid.Empty)
            return Result.Failure<RestaurantScheduleSlot>(Error.Validation("Restaurant ID is required."));

        if (opensAt >= closesAt)
            return Result.Failure<RestaurantScheduleSlot>(
                Error.Validation($"{dayOfWeek}: opens-at must be earlier than closes-at."));

        return new RestaurantScheduleSlot(restaurantId, dayOfWeek, opensAt, closesAt);
    }
}
