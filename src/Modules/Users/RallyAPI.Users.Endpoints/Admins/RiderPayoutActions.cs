using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.RiderPayoutActions;

namespace RallyAPI.Users.Endpoints.Admins;

// Pay-now is intentionally removed — rider payouts are settled manually via ICICI bulk
// transfer and reconciled by uploading the bank statement, never by a gateway button.

public class HoldRiderPayout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/rider/{payoutId:guid}/hold", HandleAsync)
            .WithName("HoldRiderPayout")
            .WithTags("Admins")
            .WithSummary("Pause a Pending rider payout (admin panel)")
            .RequireAuthorization("Admin");
    }

    public sealed record HoldRequest(string? Reason);

    private static async Task<IResult> HandleAsync(
        Guid payoutId,
        HoldRequest? request,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new HoldRiderPayoutCommand(payoutId, request?.Reason), ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}

public class ReleaseHoldRiderPayout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/rider/{payoutId:guid}/release-hold", HandleAsync)
            .WithName("ReleaseHoldRiderPayout")
            .WithTags("Admins")
            .WithSummary("Release a rider payout from hold back to Pending (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid payoutId,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new ReleaseHoldRiderPayoutCommand(payoutId), ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}

public class RetryRiderPayout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/rider/{payoutId:guid}/retry", HandleAsync)
            .WithName("RetryRiderPayout")
            .WithTags("Admins")
            .WithSummary("Re-queue a Failed rider payout for the next auto-run (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid payoutId,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new RetryRiderPayoutCommand(payoutId), ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
