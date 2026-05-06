using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.ListAdmins;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class ListAdmins : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/users", HandleAsync)
            .WithName("ListAdmins")
            .WithTags("Admins")
            .WithSummary("List admin users (SuperAdmin only). Optional filters: role (Support|CityAdmin|SuperAdmin), isActive.")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken,
        string? role = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(
            new ListAdminsQuery(adminId, role, isActive, page, pageSize),
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
