using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.ListRiders;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class ListRiders : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admins/riders", HandleAsync)
            .WithName("ListRiders")
            .WithTags("Admins")
            .WithSummary("List all riders with optional filtering by online status and KYC status")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken,
        bool? isOnline = null,
        string? kycStatus = null,
        int page = 1,
        int pageSize = 20)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(
            new ListRidersQuery(adminId, isOnline, kycStatus, page, pageSize),
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
