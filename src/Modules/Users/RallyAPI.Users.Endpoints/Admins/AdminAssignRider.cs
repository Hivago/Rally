using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Orders.Application.Commands.AdminAssignRider;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Users.Endpoints.Admins;

public class AdminAssignRider : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/orders/{orderId:guid}/assign-rider", HandleAsync)
            .WithName("AdminAssignRider")
            .WithTags("Admins")
            .WithSummary("Manually assign a rider to an order (admin panel)")
            .RequireAuthorization("Admin");
    }

    public record AdminAssignRiderRequest(Guid RiderId);

    private static async Task<IResult> HandleAsync(
        Guid orderId,
        AdminAssignRiderRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new AdminAssignRiderCommand(orderId, request.RiderId);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
