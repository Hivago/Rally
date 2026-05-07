using System.IO.Compression;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.Restaurants.Commands.ImportMenu;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.Restaurants;

public class ImportMenu : IEndpoint
{
    private const long MaxRequestBytes = 50L * 1024 * 1024; // 50 MB total upload cap

    private static readonly Dictionary<string, string> ImageContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
        };

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/restaurants/{restaurantId:guid}/menu/import", HandleAsync)
            .WithTags("Admin Catalog")
            .WithSummary("Bulk-import a restaurant menu from Excel + images zip")
            .DisableAntiforgery()
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        IFormFile menu,
        IFormFile? images,
        ISender sender,
        CancellationToken ct)
    {
        if (menu is null || menu.Length == 0)
            return Results.BadRequest(new { error = "menu file (xlsx) is required" });

        if (menu.Length + (images?.Length ?? 0) > MaxRequestBytes)
            return Results.BadRequest(new { error = "Total upload exceeds 50 MB cap" });

        var imageBlobs = images is null
            ? new Dictionary<string, ImageBlob>(StringComparer.OrdinalIgnoreCase)
            : await ExtractImagesAsync(images, ct);

        await using var workbookStream = menu.OpenReadStream();
        var command = new ImportMenuCommand(restaurantId, workbookStream, imageBlobs);

        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }

    private static async Task<IReadOnlyDictionary<string, ImageBlob>> ExtractImagesAsync(
        IFormFile zipFile,
        CancellationToken ct)
    {
        var blobs = new Dictionary<string, ImageBlob>(StringComparer.OrdinalIgnoreCase);

        await using var zipStream = zipFile.OpenReadStream();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            // Skip directories and macOS metadata files (__MACOSX/, .DS_Store)
            if (string.IsNullOrEmpty(entry.Name)) continue;
            if (entry.FullName.StartsWith("__MACOSX", StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.Name.StartsWith(".", StringComparison.Ordinal)) continue;

            var ext = Path.GetExtension(entry.Name);
            if (!ImageContentTypes.TryGetValue(ext, out var contentType)) continue;

            await using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            await entryStream.CopyToAsync(buffer, ct);

            // Index by both filename only and full path so the Excel can reference either form.
            blobs[entry.Name] = new ImageBlob(buffer.ToArray(), contentType);
            blobs[entry.FullName] = blobs[entry.Name];
        }

        return blobs;
    }
}
