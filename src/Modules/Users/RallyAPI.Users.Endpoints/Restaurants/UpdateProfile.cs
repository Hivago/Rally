using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.UpdateProfile;
using RallyAPI.Users.Application.Restaurants.Commands.UploadLogo;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

internal class UpdateProfile : IEndpoint
{
    public sealed record UpdateRestaurantProfileRequest(
        string? Name,
        string? AddressLine,
        string? Phone,
        List<string>? CuisineTypes,
        bool? IsPureVeg,
        bool? IsVeganFriendly,
        bool? HasJainOptions,
        decimal? MinOrderAmount,
        string? FssaiNumber);

    public sealed record GenerateUploadUrlRequest(string ContentType);
    public sealed record ConfirmImageRequest(string FileKey);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // PUT /api/restaurants/profile
        app.MapPut("/api/restaurants/profile", HandleUpdateProfileAsync)
            .WithName("UpdateRestaurantProfile")
            .WithTags("Restaurants")
            .WithSummary("Update restaurant profile including cuisine types, dietary attributes, and FSSAI")
            .RequireAuthorization("Restaurant");

        // POST /api/users/restaurants/{restaurantId}/logo/upload-url  (Step 1)
        app.MapPost("api/users/restaurants/{restaurantId:guid}/logo/upload-url",
        async (
            Guid restaurantId,
            [FromBody] GenerateUploadUrlRequest request,
            ISender sender,
            HttpContext httpContext) =>
        {
            var userType = httpContext.User.FindFirst("user_type")?.Value ?? "";
            var isAdmin = userType.Equals("admin", StringComparison.OrdinalIgnoreCase);
            var sub = Guid.TryParse(httpContext.User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

            var result = await sender.Send(new GenerateRestaurantLogoUploadUrlCommand(
                restaurantId, sub, isAdmin, request.ContentType));

            return result.IsSuccess
  ? Results.Ok(result.Value)
  : result.Error.ToErrorResult();
        })
        .RequireAuthorization("RestaurantOrAdmin")
        .WithName("GenerateRestaurantLogoUploadUrl")
        .WithTags("Users - Images");

        // PATCH /api/users/restaurants/{restaurantId}/logo/confirm  (Step 2)
        app.MapPatch("api/users/restaurants/{restaurantId:guid}/logo/confirm",
                async (
                    Guid restaurantId,
                    [FromBody] ConfirmImageRequest request,
                    ISender sender,
                    HttpContext httpContext) =>
                {
                    var userType = httpContext.User.FindFirst("user_type")?.Value ?? "";
                    var isAdmin = userType.Equals("admin", StringComparison.OrdinalIgnoreCase);
                    var sub = Guid.TryParse(httpContext.User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

                    var result = await sender.Send(new ConfirmRestaurantLogoCommand(
                restaurantId, sub, isAdmin, request.FileKey));

                    return result.IsSuccess
   ? Results.Ok(result.Value)
   : result.Error.ToErrorResult();
                })
                .RequireAuthorization("RestaurantOrAdmin")
                .WithName("ConfirmRestaurantLogo")
                .WithTags("Users - Images");
    }

    private static async Task<IResult> HandleUpdateProfileAsync(
        [FromBody] UpdateRestaurantProfileRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateRestaurantProfileCommand(
            restaurantId,
            request.Name,
            request.AddressLine,
            request.Phone,
            request.CuisineTypes,
            request.IsPureVeg,
            request.IsVeganFriendly,
            request.HasJainOptions,
            request.MinOrderAmount,
            request.FssaiNumber);

        var result = await sender.Send(command, ct);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Profile updated successfully." });
    }
}
