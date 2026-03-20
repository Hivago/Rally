using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Customers.Commands.UpdateProfile;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Customers;

public class UpdateProfile : IEndpoint
{

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/customers/profile", HandleAsync)
            .WithName("CustomerUpdateProfile")
            .WithTags("Customers")
            .RequireAuthorization("Customer");

    }

    public record UpdateCustomerProfileRequest(string? Name, string? Email);

    private static async Task<IResult> HandleAsync(
        UpdateCustomerProfileRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var customerId = user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(customerId, out var id))
            return Results.Unauthorized();

        var command = new UpdateCustomerProfileCommand(id, request.Name, request.Email);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Profile updated" });
    }
}