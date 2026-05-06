using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.CreateAdmin;
using RallyAPI.Users.Domain.Enums;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class CreateAdmin : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/users", HandleAsync)
            .WithName("CreateAdmin")
            .WithTags("Admins")
            .WithSummary("Create a new admin user (SuperAdmin only). Allowed roles: Support, CityAdmin.")
            .RequireAuthorization("Admin");
    }

    public record CreateAdminRequest(
        string Name,
        string Email,
        string Password,
        AdminRole Role);

    private static async Task<IResult> HandleAsync(
        CreateAdminRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new CreateAdminCommand(
            adminId,
            request.Email,
            request.Password,
            request.Name,
            request.Role);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/admin/users/{result.Value}", new { id = result.Value })
            : result.Error.ToErrorResult();
    }
}
