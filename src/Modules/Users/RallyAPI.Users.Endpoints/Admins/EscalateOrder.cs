using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Orders.Application.Commands.EscalateOrder;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Users.Endpoints.Admins;

public class EscalateOrder : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/orders/{orderId:guid}/escalate", HandleAsync)
            .WithName("AdminEscalateOrder")
            .WithTags("Admins")
            .WithSummary("Manually escalate an order to admin (admin panel)")
            .RequireAuthorization("Admin");
    }

    public record EscalateOrderRequest(string Reason);

    private static async Task<IResult> HandleAsync(
        Guid orderId,
        EscalateOrderRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new EscalateOrderCommand(orderId, request.Reason);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
