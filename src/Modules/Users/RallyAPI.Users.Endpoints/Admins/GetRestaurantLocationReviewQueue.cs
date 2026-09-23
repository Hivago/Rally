using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.ListRestaurantLocationReviews;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetRestaurantLocationReviewQueue : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admins/restaurant-location-reviews", HandleAsync)
            .WithName("AdminListRestaurantLocationReviews")
            .WithTags("Admins")
            .WithSummary("List pin-drift findings awaiting admin review (admin)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ISender sender,
        CancellationToken cancellationToken,
        RestaurantLocationReviewStatus status = RestaurantLocationReviewStatus.Pending,
        int page = 1,
        int pageSize = 20)
    {
        var query = new ListRestaurantLocationReviewsQuery(status, page, pageSize);
        var result = await sender.Send(query, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
