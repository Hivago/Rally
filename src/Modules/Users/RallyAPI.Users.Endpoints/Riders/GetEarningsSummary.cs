using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Riders.Queries.GetEarningsSummary;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Riders;

public class GetEarningsSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/riders/earnings", HandleAsync)
            .WithTags("Riders")
            .WithSummary("Get rider earnings summary (total/week/month + pending payout)")
            .RequireAuthorization("Rider");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var riderId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(new GetRiderEarningsSummaryQuery(riderId), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
