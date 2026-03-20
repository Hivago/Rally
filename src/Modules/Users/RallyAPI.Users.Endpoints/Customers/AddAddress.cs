using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Customers.Commands.AddAddress;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;

namespace RallyAPI.Users.Endpoints.Customers;

public class AddAddress : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/customers/addresses", HandleAsync)
            .WithName("CustomerAddAddress")
            .WithTags("Customers")
            .RequireAuthorization("Customer");
    }

    public record AddCustomerAddressRequest(
        string AddressLine,
        string? Landmark,
        decimal Latitude,
        decimal Longitude,
        string Label,
        bool IsDefault);

    private static async Task<IResult> HandleAsync(
        AddCustomerAddressRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var customerId = user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(customerId, out var id))
            return Results.Unauthorized();

        var command = new AddCustomerAddressCommand(
            id,
            request.AddressLine,
            request.Landmark,
            request.Latitude,
            request.Longitude,
            request.Label);
        //Removed this parameter        request.IsDefault

    var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Created($"/api/customers/addresses/{result.Value}", new { addressId = result.Value });
    }
}