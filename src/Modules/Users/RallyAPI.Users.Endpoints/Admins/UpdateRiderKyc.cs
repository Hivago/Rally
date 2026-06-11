using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.UpdateRiderKyc;
using RallyAPI.Users.Domain.Enums;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class UpdateRiderKyc : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/riders/{riderId:guid}/kyc-status", HandleAsync)
            .WithName("UpdateRiderKycStatus")
            .WithSummary("Approve or reject a rider's KYC (admin)")
            .WithTags("Admins")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid riderId,
        [FromBody] UpdateRiderKycStatusRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(
            new UpdateRiderKycCommand(adminId, riderId, request.NewKycStatus),
            cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok();
    }
}

/// <summary>Body: { "newKycStatus": "Verified" | "Rejected" | "Pending" }</summary>
public sealed record UpdateRiderKycStatusRequest(KycStatus NewKycStatus);
