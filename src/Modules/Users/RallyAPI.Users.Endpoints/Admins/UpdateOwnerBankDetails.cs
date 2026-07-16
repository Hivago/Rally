using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.UpdateBankDetails;

namespace RallyAPI.Users.Endpoints.Admins;

public class UpdateOwnerBankDetails : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/admin/owners/{ownerId:guid}/bank", HandleAsync)
            .WithName("AdminUpdateOwnerBankDetails")
            .WithTags("Admins")
            .WithSummary("Admin updates a restaurant owner's payout bank details")
            .RequireAuthorization("Admin");
    }

    public sealed record UpdateOwnerBankRequest(
        string BankAccountNumber,
        string BankIfscCode,
        string BankAccountName);

    private static async Task<IResult> HandleAsync(
        Guid ownerId,
        UpdateOwnerBankRequest request,
        ISender sender,
        CancellationToken ct)
    {
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
