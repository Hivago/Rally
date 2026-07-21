using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.UpdatePassword;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class UpdatePassword : IEndpoint
{
    public sealed record UpdatePasswordRequest(string CurrentPassword, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/owners/me/password", HandleAsync)
            .WithName("UpdateOwnerPassword")
            .WithTags("Owners")
            .WithSummary("Change account password (requires current password)")
            .RequireAuthorization("Owner");
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdatePasswordRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var ownerId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateOwnerPasswordCommand(
            ownerId,
            request.CurrentPassword,
            request.NewPassword);

        var result = await sender.Send(command, ct);
        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Password updated." });
    }
}
