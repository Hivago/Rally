using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.Users.Application.Abstractions;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetEscalatedOrders : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admins/orders/escalated", HandleAsync)
            .WithName("GetEscalatedOrders")
            .WithTags("Admins")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        IAdminRepository adminRepository,
        IEscalatedOrderQueryService escalatedOrderService,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var admin = await adminRepository.GetByIdAsync(adminId, cancellationToken);
        if (admin is null)
            return Results.NotFound(new { error = "Admin.NotFound", message = "Admin not found." });

        var result = await escalatedOrderService.GetEscalatedOrdersAsync(
            page: page < 1 ? 1 : page,
            pageSize: pageSize is < 1 or > 100 ? 20 : pageSize,
            cancellationToken);

        return Results.Ok(result);
    }
}
