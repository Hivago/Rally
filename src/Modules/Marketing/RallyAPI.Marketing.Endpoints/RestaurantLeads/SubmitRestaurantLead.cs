using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Marketing.Application.RestaurantLeads.Commands.SubmitLead;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Marketing.Endpoints.RestaurantLeads;

public sealed class SubmitRestaurantLead : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/restaurant-leads", HandleAsync)
            .WithName("SubmitRestaurantLead")
            .WithTags("Marketing")
            .WithSummary("Public landing page endpoint: capture a restaurant interest lead.")
            .AllowAnonymous()
            .RequireRateLimiting("lead-capture");
    }

    public record SubmitRestaurantLeadRequest(
        string RestaurantName,
        string OwnerName,
        string Phone,
        string City,
        int DailyOrders,
        string? Source);

    private static async Task<IResult> HandleAsync(
        SubmitRestaurantLeadRequest request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();

        var command = new SubmitRestaurantLeadCommand(
            request.RestaurantName,
            request.OwnerName,
            request.Phone,
            request.City,
            request.DailyOrders,
            request.Source,
            ip);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/restaurant-leads/{result.Value.Id}", result.Value)
            : result.Error.ToErrorResult();
    }
}
