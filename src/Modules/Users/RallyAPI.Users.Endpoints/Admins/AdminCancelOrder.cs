using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Orders.Application.Commands.CancelOrder;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Extensions;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class AdminCancelOrder : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/orders/{orderId:guid}/cancel", HandleAsync)
            .WithName("AdminCancelOrder")
            .WithTags("Admins")
            .WithSummary("Admin cancel — bypasses the normal status guard (admin panel)")
            .RequireAuthorization("Admin");
    }

    public record AdminCancelOrderRequest(
        CancellationReason Reason,
        string? Notes,
        bool ForceCancel = true);

    private static async Task<IResult> HandleAsync(
        Guid orderId,
        AdminCancelOrderRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new CancelOrderCommand
        {
            OrderId = orderId,
            CancelledBy = adminId,
            CallerRole = "Admin",
            Reason = request.Reason,
            Notes = request.Notes,
            ForceCancel = request.ForceCancel
        };

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
