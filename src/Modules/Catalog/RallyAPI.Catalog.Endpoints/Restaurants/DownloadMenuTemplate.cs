using ClosedXML.Excel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace RallyAPI.Catalog.Endpoints.Restaurants;

public class DownloadMenuTemplate : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/menu/template.xlsx", Handle)
            .WithTags("Admin Catalog")
            .WithSummary("Download the bulk-import Excel template")
            .RequireAuthorization("Admin");
    }

    private static IResult Handle()
    {
        using var workbook = new XLWorkbook();

        var menus = workbook.Worksheets.Add("Menus");
        WriteHeader(menus, new[] { "Name", "Description", "DisplayOrder" });
        menus.Cell(2, 1).Value = "Main Menu";
        menus.Cell(2, 2).Value = "All-day items";
        menus.Cell(2, 3).Value = 1;

        var items = workbook.Worksheets.Add("Items");
        WriteHeader(items, new[]
        {
            "MenuName", "ItemName", "Description", "BasePrice",
            "IsVegetarian", "PrepMinutes", "DisplayOrder", "Tags",
            "ImageFilename"
        });
        items.Cell(2, 1).Value = "Main Menu";
        items.Cell(2, 2).Value = "Margherita Pizza";
        items.Cell(2, 3).Value = "Classic tomato, mozzarella, basil";
        items.Cell(2, 4).Value = 299;
        items.Cell(2, 5).Value = true;
        items.Cell(2, 6).Value = 20;
        items.Cell(2, 7).Value = 1;
        items.Cell(2, 8).Value = "pizza, bestseller";
        items.Cell(2, 9).Value = "margherita.jpg";

        var groups = workbook.Worksheets.Add("OptionGroups");
        WriteHeader(groups, new[]
        {
            "ItemName", "GroupName", "IsRequired",
            "MinSelections", "MaxSelections", "DisplayOrder"
        });
        groups.Cell(2, 1).Value = "Margherita Pizza";
        groups.Cell(2, 2).Value = "Size";
        groups.Cell(2, 3).Value = true;
        groups.Cell(2, 4).Value = 1;
        groups.Cell(2, 5).Value = 1;
        groups.Cell(2, 6).Value = 1;

        var options = workbook.Worksheets.Add("Options");
        WriteHeader(options, new[]
        {
            "ItemName", "GroupName", "OptionName",
            "OptionType", "AdditionalPrice", "IsDefault"
        });
        options.Cell(2, 1).Value = "Margherita Pizza";
        options.Cell(2, 2).Value = "Size";
        options.Cell(2, 3).Value = "Regular";
        options.Cell(2, 4).Value = "Size";
        options.Cell(2, 5).Value = 0;
        options.Cell(2, 6).Value = true;
        options.Cell(3, 1).Value = "Margherita Pizza";
        options.Cell(3, 2).Value = "Size";
        options.Cell(3, 3).Value = "Large";
        options.Cell(3, 4).Value = "Size";
        options.Cell(3, 5).Value = 150;
        options.Cell(3, 6).Value = false;

        foreach (var sheet in workbook.Worksheets)
            sheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return Results.File(
            stream,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileDownloadName: "rally-menu-template.xlsx");
    }

    private static void WriteHeader(IXLWorksheet sheet, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }
    }
}
