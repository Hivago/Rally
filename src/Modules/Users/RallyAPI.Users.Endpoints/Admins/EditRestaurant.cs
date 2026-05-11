using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.EditRestaurant;

namespace RallyAPI.Users.Endpoints.Admins;

public class EditRestaurant : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/admins/restaurants/{restaurantId:guid}", HandleAsync)
            .WithName("AdminEditRestaurant")
            .WithTags("Admins")
            .WithSummary("Edit a restaurant's details (admin)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        EditRestaurantRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new EditRestaurantCommand(
            restaurantId,
            request.Name,
            request.Phone,
            request.AddressLine,
            // request.CommissionPercentage,   // Deprecated: percentage commission no longer supported. Only flat fee is used.
            request.CommissionFlatFee,
            request.AvgPrepTimeMins,
            request.CuisineTypes,
            request.IsPureVeg,
            request.IsVeganFriendly,
            request.HasJainOptions,
            request.MinOrderAmount,
            request.FssaiNumber);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}

public record EditRestaurantRequest(
    string? Name,
    string? Phone,
    string? AddressLine,
    // decimal? CommissionPercentage,   // Deprecated: percentage commission no longer supported. Only flat fee is used.
    decimal? CommissionFlatFee,
    int? AvgPrepTimeMins,
    List<string>? CuisineTypes,
    bool? IsPureVeg,
    bool? IsVeganFriendly,
    bool? HasJainOptions,
    decimal? MinOrderAmount,
    string? FssaiNumber);
