using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.CreateOwner;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class CreateOwner : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/owners", HandleAsync)
            .WithName("CreateOwner")
            .WithTags("Admins")
            .WithSummary("Create a new restaurant owner. OwnerId returned here is required when creating outlets via POST /api/admin/restaurants.")
            .RequireAuthorization("Admin");
    }

    public record CreateOwnerRequest(
        string Name,
        string Email,
        string Phone,
        string Password,
        string? PanNumber,
        string? GstNumber,
        string? BankAccountNumber,
        string? BankIfscCode,
        string? BankAccountName);

    private static async Task<IResult> HandleAsync(
        CreateOwnerRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new CreateOwnerCommand(
            adminId,
            request.Name,
            request.Email,
            request.Phone,
            request.Password,
            request.PanNumber,
            request.GstNumber,
            request.BankAccountNumber,
            request.BankIfscCode,
            request.BankAccountName);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/admin/owners/{result.Value.OwnerId}", result.Value)
            : result.Error.ToErrorResult();
    }
}
