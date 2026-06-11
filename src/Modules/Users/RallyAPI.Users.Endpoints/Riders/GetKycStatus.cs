using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Riders.Queries.GetKycStatus;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Riders;

public class GetKycStatus : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/riders/kyc-status", HandleAsync)
            .WithTags("Riders")
            .WithSummary("Get the rider's own KYC status and uploaded documents")
            .RequireAuthorization("Rider");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var riderId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(new GetRiderKycStatusQuery(riderId), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
