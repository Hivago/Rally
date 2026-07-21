using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Orders.Application.Commands.AdminPayoutActions;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Users.Endpoints.Admins;

// Pay-now is intentionally removed — restaurant payouts are settled manually via ICICI
// bulk transfer and reconciled by uploading the bank statement, never by a gateway button.

public class HoldRestaurantPayout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/restaurant/{payoutId:guid}/hold", HandleAsync)
            .WithName("HoldRestaurantPayout")
            .WithTags("Admins")
            .WithSummary("Pause a Pending payout (admin panel)")
            .RequireAuthorization("Admin");
    }

    public record HoldRequest(string? Reason);

    private static async Task<IResult> HandleAsync(
        Guid payoutId,
        HoldRequest? request,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new HoldRestaurantPayoutCommand(payoutId, request?.Reason), ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}

public class ReleaseHoldRestaurantPayout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/restaurant/{payoutId:guid}/release-hold", HandleAsync)
            .WithName("ReleaseHoldRestaurantPayout")
            .WithTags("Admins")
            .WithSummary("Release a payout from hold back to Pending (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid payoutId,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new ReleaseHoldRestaurantPayoutCommand(payoutId), ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}

public class RetryRestaurantPayout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/restaurant/{payoutId:guid}/retry", HandleAsync)
            .WithName("RetryRestaurantPayout")
            .WithTags("Admins")
            .WithSummary("Re-queue a Failed payout for the next auto-run (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid payoutId,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new RetryRestaurantPayoutCommand(payoutId), ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
