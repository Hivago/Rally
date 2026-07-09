using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Users.Application.Admins.Commands.ReleaseRiderDelivery;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Users.Endpoints.Admins;

public class AdminReleaseRider : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/riders/{riderId:guid}/release-delivery", HandleAsync)
            .WithName("AdminReleaseRiderDelivery")
            .WithTags("Admins")
            .WithSummary("Force-clear a rider's stuck delivery so they receive new offers (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid riderId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ReleaseRiderDeliveryCommand(riderId), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
