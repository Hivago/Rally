using FluentValidation;

namespace RallyAPI.Users.Application.Owners.Commands.QuickPauseOutlet;

public sealed class QuickPauseOutletCommandValidator : AbstractValidator<QuickPauseOutletCommand>
{
    public QuickPauseOutletCommandValidator()
    {
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 1440)
            .WithMessage("Pause duration must be between 1 minute and 24 hours.");
        RuleFor(x => x.Reason)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}
