using FluentValidation;

namespace RallyAPI.Users.Application.Owners.Commands.ScheduleTimeOff;

public sealed class ScheduleTimeOffCommandValidator : AbstractValidator<ScheduleTimeOffCommand>
{
    public ScheduleTimeOffCommandValidator()
    {
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.StartsAtUtc).NotEmpty();
        RuleFor(x => x.EndsAtUtc).NotEmpty();
        RuleFor(x => x.EndsAtUtc)
            .GreaterThan(x => x.StartsAtUtc)
            .WithMessage("End time must be after start time.");
        RuleFor(x => x.Reason)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}
