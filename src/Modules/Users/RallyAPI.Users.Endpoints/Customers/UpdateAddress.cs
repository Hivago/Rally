// File: src/Modules/Users/RallyAPI.Users.Endpoints/Customers/UpdateAddress.cs

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Customers.Commands.UpdateAddress;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Customers;

public class UpdateAddress : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/customers/addresses/{addressId:guid}", HandleAsync)
            .WithName("CustomerUpdateAddress")
            .WithTags("Customers")
            .RequireAuthorization("Customer");
    }

    public record UpdateAddressRequest(
        string AddressLine,
        string? Landmark,
        decimal Latitude,
        decimal Longitude,
        string Label);

    private static async Task<IResult> HandleAsync(
        Guid addressId,
        UpdateAddressRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var customerId = user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(customerId, out var id))
            return Results.Unauthorized();

        var command = new UpdateCustomerAddressCommand(
            id,
            addressId,
            request.AddressLine,
            request.Landmark,
            request.Latitude,
            request.Longitude,
            request.Label);

        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Address updated" });
    }
}
