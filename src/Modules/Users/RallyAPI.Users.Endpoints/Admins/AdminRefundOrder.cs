using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Orders.Application.Commands.RefundPayment;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Users.Endpoints.Admins;

public class AdminRefundOrder : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/orders/{orderId:guid}/refund", HandleAsync)
            .WithName("AdminRefundOrder")
            .WithTags("Admins")
            .WithSummary("Admin refund — bypasses the normal refundable-status guard (admin panel)")
            .RequireAuthorization("Admin");
    }

    public record AdminRefundOrderRequest(
        decimal? Amount,
        bool ForceRefund = true);

    private static async Task<IResult> HandleAsync(
        Guid orderId,
        AdminRefundOrderRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RefundPaymentCommand(
            orderId,
            request.Amount,
            CallerRole: "Admin",
            ForceRefund: request.ForceRefund);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
