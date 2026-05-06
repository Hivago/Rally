using FluentValidation;

namespace RallyAPI.Catalog.Application.Restaurants.Commands.ImportMenu;

public sealed class ImportMenuCommandValidator : AbstractValidator<ImportMenuCommand>
{
    public ImportMenuCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.WorkbookStream).NotNull();
        RuleFor(x => x.ImageBlobs).NotNull();
    }
}
