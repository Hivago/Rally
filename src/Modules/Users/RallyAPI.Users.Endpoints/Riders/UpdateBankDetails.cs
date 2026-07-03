using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Riders.Commands.UpdateBankDetails;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Riders;

public class UpdateBankDetails : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/riders/bank", HandleAsync)
            .WithTags("Riders")
            .WithSummary("Rider updates their payout bank details")
            .RequireAuthorization("Rider");
    }

    public sealed record UpdateRiderBankRequest(
        string BankAccountNumber,
        string BankIfscCode,
        string BankAccountName);

    private static async Task<IResult> HandleAsync(
        UpdateRiderBankRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var riderId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateRiderBankDetailsCommand(
            riderId,
            request.BankAccountNumber,
            request.BankIfscCode,
            request.BankAccountName);

        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new { message = "Bank details updated successfully" })
            : result.Error.ToErrorResult();
    }
}
