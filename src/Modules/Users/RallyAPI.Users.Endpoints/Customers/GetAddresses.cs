// File: src/Modules/Users/RallyAPI.Users.Endpoints/Customers/GetAddresses.cs

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Customers.Queries.GetAddresses;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Customers;

public class GetAddresses : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/customers/addresses", HandleAsync)
            .WithName("CustomerGetAddresses")
            .WithTags("Customers")
            .RequireAuthorization("Customer");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var customerId = user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(customerId, out var id))
            return Results.Unauthorized();

        var query = new GetCustomerAddressesQuery(id);
        var result = await sender.Send(query, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
