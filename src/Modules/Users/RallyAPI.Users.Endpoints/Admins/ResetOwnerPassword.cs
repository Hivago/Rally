using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.ResetOwnerPassword;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class ResetOwnerPassword : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/owners/{ownerId:guid}/reset-password", HandleAsync)
            .WithName("ResetOwnerPassword")
            .WithTags("Admins")
            .WithSummary("Force-reset a restaurant owner's password. Returns a new temporary password once — relay it to the owner out of band.")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid ownerId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new ResetOwnerPasswordCommand(adminId, ownerId);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
