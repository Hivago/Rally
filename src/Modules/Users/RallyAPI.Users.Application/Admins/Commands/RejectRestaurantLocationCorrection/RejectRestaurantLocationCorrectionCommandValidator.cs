using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.RejectRestaurantLocationCorrection;

public sealed class RejectRestaurantLocationCorrectionCommandValidator
    : AbstractValidator<RejectRestaurantLocationCorrectionCommand>
{
    public RejectRestaurantLocationCorrectionCommandValidator()
    {
        RuleFor(x => x.QueueEntryId).NotEmpty();
        RuleFor(x => x.AdminId).NotEmpty();
    }
}
