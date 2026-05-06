using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.ChangePassword;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class ChangeAdminPassword : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/admin/profile/password", HandleAsync)
            .WithName("ChangeAdminPassword")
            .WithTags("Admins")
            .WithSummary("Change the signed-in admin's password. Requires current password.")
            .RequireAuthorization("Admin");
    }

    public record ChangeAdminPasswordRequest(
        string CurrentPassword,
        string NewPassword,
        string ConfirmNewPassword);

    private static async Task<IResult> HandleAsync(
        ChangeAdminPasswordRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new ChangeAdminPasswordCommand(
            adminId,
            request.CurrentPassword,
            request.NewPassword,
            request.ConfirmNewPassword);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
