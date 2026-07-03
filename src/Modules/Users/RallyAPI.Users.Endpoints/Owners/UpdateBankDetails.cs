using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.UpdateBankDetails;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class UpdateBankDetails : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/owners/me/bank", HandleAsync)
            .WithName("UpdateOwnerBankDetails")
            .WithTags("Owners")
            .WithSummary("Owner updates their own payout bank details")
            .RequireAuthorization("Owner");
    }

    public sealed record UpdateOwnerBankRequest(
        string BankAccountNumber,
        string BankIfscCode,
        string BankAccountName);

    private static async Task<IResult> HandleAsync(
        UpdateOwnerBankRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var ownerId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateOwnerBankDetailsCommand(
            ownerId,
            request.BankAccountNumber,
            request.BankIfscCode,
            request.BankAccountName);

        var result = await sender.Send(command, ct);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Bank details updated successfully" });
    }
}
