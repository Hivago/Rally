using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Users.Application.Queries.RiderOverview;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetRiderOverview : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/riders/{riderId:guid}/overview", HandleAsync)
            .WithName("RiderOverview")
            .WithTags("Admins")
            .WithSummary("Get a detailed overview of a rider (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid riderId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new RiderOverviewQuery(riderId);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}