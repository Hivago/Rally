using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateHours;

public sealed class UpdateRestaurantHoursCommandValidator
    : AbstractValidator<UpdateRestaurantHoursCommand>
{
    public UpdateRestaurantHoursCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();

        When(x => x.OpeningTime.HasValue || x.ClosingTime.HasValue, () =>
        {
            RuleFor(x => x.OpeningTime).NotNull();
            RuleFor(x => x.ClosingTime).NotNull();
            RuleFor(x => x)
                .Must(x => x.OpeningTime!.Value < x.ClosingTime!.Value)
                .WithMessage("Opening time must be before closing time.")
                .When(x => x.OpeningTime.HasValue && x.ClosingTime.HasValue);
        });

        RuleForEach(x => x.WeeklySchedule!).ChildRules(day =>
        {
            day.RuleFor(d => d.DayOfWeek).IsInEnum();
            day.RuleFor(d => d.Slots).NotNull();
            day.RuleFor(d => d.Slots!.Count)
                .LessThanOrEqualTo(3)
                .WithMessage("At most 3 slots per day.")
                .When(d => d.Slots is not null);
        }).When(x => x.WeeklySchedule is not null);
    }
}
