using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.ApproveRestaurantLocationCorrection;

public sealed class ApproveRestaurantLocationCorrectionCommandValidator
    : AbstractValidator<ApproveRestaurantLocationCorrectionCommand>
{
    public ApproveRestaurantLocationCorrectionCommandValidator()
    {
        RuleFor(x => x.QueueEntryId).NotEmpty();
        RuleFor(x => x.AdminId).NotEmpty();
    }
}
