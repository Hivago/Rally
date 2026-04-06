using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Domain.MenuItems;

public static class OptionGroupErrors
{
    public static readonly Error NotFound = Error.Create(
        "OptionGroup.NotFound",
        "Option group not found");

    public static readonly Error OptionNotFound = Error.Create(
        "OptionGroup.OptionNotFound",
        "Option not found");

    public static readonly Error GroupNameRequired = Error.Create(
        "OptionGroup.GroupNameRequired",
        "Option group name is required");

    public static readonly Error InvalidSelections = Error.Create(
        "OptionGroup.InvalidSelections",
        "Max selections must be greater than or equal to min selections");
}
