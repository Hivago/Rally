using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.SetOwnerCrossOutletAccept;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class SetOwnerCrossOutletAccept : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/admin/owners/{ownerId:guid}/cross-outlet-accept", HandleAsync)
            .WithName("SetOwnerCrossOutletAccept")
            .WithTags("Admins")
            .WithSummary("Enable or disable multi-outlet cross-accept for a restaurant owner (off by default)")
            .RequireAuthorization("Admin");
    }

    public sealed record SetCrossOutletAcceptRequest(bool Enabled);

    private static async Task<IResult> HandleAsync(
        Guid ownerId,
        SetCrossOutletAcceptRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new SetOwnerCrossOutletAcceptCommand(adminId, ownerId, request.Enabled);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
