using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Commands.CreateMenuItem;
using RallyAPI.Catalog.Application.MenuItems.Commands.GenerateMenuItemUploadUrl;
using RallyAPI.Catalog.Application.MenuItems.Commands.UpdateMenuItem;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RallyAPI.Catalog.Application.MenuItems.Commands.ConfirmMenuItemImage;


namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class UpdateMenuItem : IEndpoint
{

    // ──────────────────────────────────────────────────────────────
    // Request/Response DTOs (endpoint-layer only)
    // ──────────────────────────────────────────────────────────────

    /// <summary>Body for the upload-url endpoint.</summary>
    public sealed record GenerateUploadUrlRequest(
        /// <summary>Must be one of: image/jpeg, image/png, image/webp</summary>
        string ContentType
    );

    /// <summary>Body for the confirm endpoint.</summary>
    public sealed record ConfirmImageRequest(
        /// <summary>The fileKey returned by the upload-url endpoint.</summary>
        string FileKey
    );

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/restaurant/items/{itemId:guid}", HandleAsync)
            .WithTags("Restaurant Menu Items")
            .WithSummary("Update a menu item")
            .RequireAuthorization("Restaurant");

        app.MapPost("api/catalog/menu-items/{menuItemId:guid}/image/upload-url",
    async (
        Guid menuItemId,
        [FromBody] GenerateUploadUrlRequest request,
        ISender sender,
        HttpContext httpContext) =>
    {
        var (restaurantId, isAdmin) = ExtractCallerContext(httpContext);

        var command = new GenerateMenuItemUploadUrlCommand(
            menuItemId,
            restaurantId,
            isAdmin,
            request.ContentType);

        var result = await sender.Send(command);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message });
    })
    .RequireAuthorization("RestaurantOrAdmin")
    .WithName("GenerateMenuItemUploadUrl")
    .WithTags("Catalog - Images")
    .Produces<GenerateMenuItemUploadUrlResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status422UnprocessableEntity);

        app.MapPatch("api/catalog/menu-items/{menuItemId:guid}/image/confirm",
    async (
        Guid menuItemId,
        [FromBody] ConfirmImageRequest request,
        ISender sender,
        HttpContext httpContext) =>
    {
        var (restaurantId, isAdmin) = ExtractCallerContext(httpContext);

        var command = new ConfirmMenuItemImageCommand(
            menuItemId,
            restaurantId,
            isAdmin,
            request.FileKey);

        var result = await sender.Send(command);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message });
    })
    .RequireAuthorization("RestaurantOrAdmin")
    .WithName("ConfirmMenuItemImage")
    .WithTags("Catalog - Images")
    .Produces<ConfirmMenuItemImageResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound);


    }

    // ──────────────────────────────────────────────────────────────
    // Helper: extracts restaurantId and isAdmin from JWT claims.
    //
    // ADD this private helper to your CatalogEndpoints class,
    // or inline the claim extraction if you already have a pattern for it.
    //
    // JWT claims set by JwtProvider:
    //   "sub"       → user ID (Guid)
    //   "user_type" → "restaurant" | "admin"
    // ──────────────────────────────────────────────────────────────
    static (Guid restaurantId, bool isAdmin) ExtractCallerContext(HttpContext httpContext)
    {
        var userType = httpContext.User.FindFirst("user_type")?.Value ?? string.Empty;
        var isAdmin = userType.Equals("admin", StringComparison.OrdinalIgnoreCase);

        var subClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var restaurantId = Guid.TryParse(subClaim, out var id) ? id : Guid.Empty;

        return (restaurantId, isAdmin);
    }

    private static async Task<IResult> HandleAsync(
        Guid itemId,
        UpdateMenuItemRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var command = new UpdateMenuItemCommand(
            itemId,
            restaurantId,
            request.Name,
            request.Description,
            request.BasePrice,
            request.ImageUrl,
            request.DisplayOrder,
            request.IsVegetarian,
            request.PreparationTimeMinutes,
            request.Options?.Select(o => new MenuItemOptionDto(
                o.Name,
                o.Type,
                o.AdditionalPrice,
                o.IsDefault)).ToList());

        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new { message = "Menu item updated successfully" })
            : Results.BadRequest(result.Error);
    }
}

public record UpdateMenuItemRequest(
    string Name,
    string? Description,
    decimal BasePrice,
    string? ImageUrl,
    int DisplayOrder,
    bool IsVegetarian,
    int PreparationTimeMinutes,
    List<MenuItemOptionRequest>? Options);