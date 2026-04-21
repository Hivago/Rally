using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateDietary;

public sealed class UpdateRestaurantDietaryCommandValidator
    : AbstractValidator<UpdateRestaurantDietaryCommand>
{
    public UpdateRestaurantDietaryCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();

        RuleFor(x => x.DietaryType)
            .IsInEnum()
            .When(x => x.DietaryType.HasValue);

        RuleFor(x => x.CuisineTypes)
            .Must(c => c!.Count <= 20)
            .WithMessage("Maximum 20 cuisine types allowed.")
            .When(x => x.CuisineTypes is not null);
    }
}
