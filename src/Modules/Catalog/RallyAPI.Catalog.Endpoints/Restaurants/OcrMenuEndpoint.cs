using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.Restaurants.Commands.OcrMenu;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.Restaurants;

public class OcrMenuEndpoint : IEndpoint
{
    private const long MaxFileBytes = 10L * 1024 * 1024; // 10 MB per image

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf",
    };

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/menu/ocr-extract", HandleAsync)
            .WithTags("Admin Catalog")
            .WithSummary("OCR a menu image and return suggested item rows")
            .DisableAntiforgery()
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        IFormFile image,
        ISender sender,
        CancellationToken ct)
    {
        if (image is null || image.Length == 0)
            return Results.BadRequest(new { error = "image file is required" });

        if (image.Length > MaxFileBytes)
            return Results.BadRequest(new { error = "image exceeds 10 MB cap" });

        if (!AllowedContentTypes.Contains(image.ContentType ?? string.Empty))
            return Results.BadRequest(new { error = $"unsupported content-type: {image.ContentType}" });

        await using var stream = image.OpenReadStream();
        var result = await sender.Send(new OcrMenuCommand(stream), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
