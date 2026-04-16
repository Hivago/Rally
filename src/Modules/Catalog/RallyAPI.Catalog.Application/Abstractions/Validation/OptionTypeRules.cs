using RallyAPI.Catalog.Domain.Enums;

namespace RallyAPI.Catalog.Application.Abstractions.Validation;

internal static class OptionTypeRules
{
    public static readonly string AllowedTypes =
        string.Join(", ", Enum.GetNames(typeof(OptionType)));

    public static bool IsValid(string? type) =>
        !string.IsNullOrWhiteSpace(type) &&
        Enum.TryParse<OptionType>(type, ignoreCase: true, out _);
}
