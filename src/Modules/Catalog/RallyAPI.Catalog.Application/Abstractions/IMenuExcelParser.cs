namespace RallyAPI.Catalog.Application.Abstractions;

/// <summary>
/// Parses an Excel workbook into the strongly-typed <see cref="ParsedMenuWorkbook"/> shape
/// used by the bulk-import handler. Pure parsing — no DB writes, no image uploads.
/// </summary>
public interface IMenuExcelParser
{
    ParsedMenuWorkbook Parse(Stream xlsxStream);
}

public sealed record ParsedMenuWorkbook(
    IReadOnlyList<ParsedMenuRow> Menus,
    IReadOnlyList<ParsedItemRow> Items,
    IReadOnlyList<ParsedOptionGroupRow> OptionGroups,
    IReadOnlyList<ParsedOptionRow> Options,
    IReadOnlyList<string> ParseErrors);

public sealed record ParsedMenuRow(
    int RowNumber,
    string Name,
    string? Description,
    int DisplayOrder);

public sealed record ParsedItemRow(
    int RowNumber,
    string MenuName,
    string ItemName,
    string? Description,
    decimal BasePrice,
    bool IsVegetarian,
    int PreparationTimeMinutes,
    int DisplayOrder,
    string? ImageFilename,
    IReadOnlyList<string> Tags);

public sealed record ParsedOptionGroupRow(
    int RowNumber,
    string ItemName,
    string GroupName,
    bool IsRequired,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder);

public sealed record ParsedOptionRow(
    int RowNumber,
    string ItemName,
    string GroupName,
    string OptionName,
    string OptionType,
    decimal AdditionalPrice,
    bool IsDefault);
