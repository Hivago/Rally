using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateOperations;

public sealed class UpdateRestaurantOperationsCommandValidator
    : AbstractValidator<UpdateRestaurantOperationsCommand>
{
    public UpdateRestaurantOperationsCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();

        RuleFor(x => x.AvgPrepTimeMins)
            .InclusiveBetween(5, 120)
            .When(x => x.AvgPrepTimeMins.HasValue);

        RuleFor(x => x.MinOrderAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinOrderAmount.HasValue);
    }
}
