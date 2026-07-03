using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Riders.Commands.UpdateBankDetails;

namespace RallyAPI.Users.Endpoints.Admins;

public class UpdateRiderBankDetails : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/admin/riders/{riderId:guid}/bank", HandleAsync)
            .WithName("AdminUpdateRiderBankDetails")
            .WithTags("Admins")
            .WithSummary("Admin updates a rider's payout bank details")
            .RequireAuthorization("Admin");
    }

    public sealed record UpdateRiderBankRequest(
        string BankAccountNumber,
        string BankIfscCode,
        string BankAccountName);

    private static async Task<IResult> HandleAsync(
        Guid riderId,
        UpdateRiderBankRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var command = new UpdateRiderBankDetailsCommand(
            riderId,
            request.BankAccountNumber,
            request.BankIfscCode,
            request.BankAccountName);

        var result = await sender.Send(command, ct);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Bank details updated successfully" });
    }
}
