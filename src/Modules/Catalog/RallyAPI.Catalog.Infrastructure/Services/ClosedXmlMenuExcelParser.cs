using ClosedXML.Excel;
using RallyAPI.Catalog.Application.Abstractions;

namespace RallyAPI.Catalog.Infrastructure.Services;

/// <summary>
/// Parses the bulk-import Excel template into typed rows.
/// Expected sheets: "Menus", "Items", "OptionGroups", "Options".
/// Header row = row 1 on every sheet. Data starts on row 2.
/// </summary>
public sealed class ClosedXmlMenuExcelParser : IMenuExcelParser
{
    public ParsedMenuWorkbook Parse(Stream xlsxStream)
    {
        var errors = new List<string>();

        using var workbook = new XLWorkbook(xlsxStream);

        var menus = ParseMenusSheet(workbook, errors);
        var items = ParseItemsSheet(workbook, errors);
        var groups = ParseOptionGroupsSheet(workbook, errors);
        var options = ParseOptionsSheet(workbook, errors);

        return new ParsedMenuWorkbook(menus, items, groups, options, errors);
    }

    private static IReadOnlyList<ParsedMenuRow> ParseMenusSheet(IXLWorkbook wb, List<string> errors)
    {
        if (!wb.TryGetWorksheet("Menus", out var sheet))
        {
            errors.Add("Sheet 'Menus' is missing.");
            return Array.Empty<ParsedMenuRow>();
        }

        var result = new List<ParsedMenuRow>();
        foreach (var row in DataRows(sheet))
        {
            var name = GetString(row.Cell(1));
            if (string.IsNullOrWhiteSpace(name)) continue;

            result.Add(new ParsedMenuRow(
                RowNumber: row.RowNumber(),
                Name: name,
                Description: GetString(row.Cell(2)),
                DisplayOrder: GetInt(row.Cell(3), defaultValue: 0)));
        }
        return result;
    }

    private static IReadOnlyList<ParsedItemRow> ParseItemsSheet(IXLWorkbook wb, List<string> errors)
    {
        if (!wb.TryGetWorksheet("Items", out var sheet))
        {
            errors.Add("Sheet 'Items' is missing.");
            return Array.Empty<ParsedItemRow>();
        }

        var result = new List<ParsedItemRow>();
        foreach (var row in DataRows(sheet))
        {
            var menuName = GetString(row.Cell(1));
            var itemName = GetString(row.Cell(2));
            if (string.IsNullOrWhiteSpace(menuName) || string.IsNullOrWhiteSpace(itemName)) continue;

            var priceCell = row.Cell(4);
            if (!TryGetDecimal(priceCell, out var price))
            {
                errors.Add($"Items row {row.RowNumber()}: BasePrice is missing or invalid.");
                continue;
            }

            var tagsRaw = GetString(row.Cell(8)) ?? string.Empty;
            var tags = tagsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            result.Add(new ParsedItemRow(
                RowNumber: row.RowNumber(),
                MenuName: menuName,
                ItemName: itemName,
                Description: GetString(row.Cell(3)),
                BasePrice: price,
                IsVegetarian: GetBool(row.Cell(5)),
                PreparationTimeMinutes: GetInt(row.Cell(6), defaultValue: 15),
                DisplayOrder: GetInt(row.Cell(7), defaultValue: 0),
                Tags: tags,
                ImageFilename: GetString(row.Cell(9))));
        }
        return result;
    }

    private static IReadOnlyList<ParsedOptionGroupRow> ParseOptionGroupsSheet(IXLWorkbook wb, List<string> errors)
    {
        if (!wb.TryGetWorksheet("OptionGroups", out var sheet))
        {
            return Array.Empty<ParsedOptionGroupRow>();
        }

        var result = new List<ParsedOptionGroupRow>();
        foreach (var row in DataRows(sheet))
        {
            var itemName = GetString(row.Cell(1));
            var groupName = GetString(row.Cell(2));
            if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(groupName)) continue;

            result.Add(new ParsedOptionGroupRow(
                RowNumber: row.RowNumber(),
                ItemName: itemName,
                GroupName: groupName,
                IsRequired: GetBool(row.Cell(3)),
                MinSelections: GetInt(row.Cell(4), defaultValue: 0),
                MaxSelections: GetInt(row.Cell(5), defaultValue: 1),
                DisplayOrder: GetInt(row.Cell(6), defaultValue: 0)));
        }
        return result;
    }

    private static IReadOnlyList<ParsedOptionRow> ParseOptionsSheet(IXLWorkbook wb, List<string> errors)
    {
        if (!wb.TryGetWorksheet("Options", out var sheet))
        {
            return Array.Empty<ParsedOptionRow>();
        }

        var result = new List<ParsedOptionRow>();
        foreach (var row in DataRows(sheet))
        {
            var itemName = GetString(row.Cell(1));
            var groupName = GetString(row.Cell(2));
            var optionName = GetString(row.Cell(3));
            if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(optionName))
                continue;

            if (!TryGetDecimal(row.Cell(5), out var addPrice)) addPrice = 0m;

            result.Add(new ParsedOptionRow(
                RowNumber: row.RowNumber(),
                ItemName: itemName,
                GroupName: groupName,
                OptionName: optionName,
                OptionType: GetString(row.Cell(4)) ?? "Choice",
                AdditionalPrice: addPrice,
                IsDefault: GetBool(row.Cell(6))));
        }
        return result;
    }

    private static IEnumerable<IXLRow> DataRows(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed();
        if (lastRow is null) yield break;
        for (int i = 2; i <= lastRow.RowNumber(); i++)
            yield return sheet.Row(i);
    }

    private static string? GetString(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        var v = cell.GetString().Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static int GetInt(IXLCell cell, int defaultValue)
    {
        if (cell.IsEmpty()) return defaultValue;
        return cell.TryGetValue<int>(out var v) ? v : defaultValue;
    }

    private static bool GetBool(IXLCell cell)
    {
        if (cell.IsEmpty()) return false;
        if (cell.TryGetValue<bool>(out var b)) return b;
        var s = cell.GetString().Trim().ToLowerInvariant();
        return s is "y" or "yes" or "true" or "1";
    }

    private static bool TryGetDecimal(IXLCell cell, out decimal value)
    {
        if (cell.IsEmpty()) { value = 0; return false; }
        return cell.TryGetValue(out value);
    }
}
