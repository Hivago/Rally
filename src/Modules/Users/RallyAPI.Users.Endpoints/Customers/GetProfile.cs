using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Customers.Queries.GetProfile;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;

namespace RallyAPI.Users.Endpoints.Customers;

public class GetProfile : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/customers/profile", HandleAsync)
            .WithName("CustomerGetProfile")
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

        var query = new GetCustomerProfileQuery(id);
        var result = await sender.Send(query, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}