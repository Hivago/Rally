using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Marketing.Application.Waitlist.Commands.JoinWaitlist;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Marketing.Endpoints.Waitlist;

public sealed class JoinWaitlist : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/waitlist", HandleAsync)
            .WithName("JoinCustomerWaitlist")
            .WithTags("Marketing")
            .WithSummary("Public landing page endpoint: capture a customer waitlist signup.")
            .AllowAnonymous()
            .RequireRateLimiting("lead-capture");
    }

    public record JoinWaitlistRequest(
        string Name,
        string Email,
        string Phone,
        string? Source);

    private static async Task<IResult> HandleAsync(
        JoinWaitlistRequest request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();

        var command = new JoinWaitlistCommand(
            request.Name,
            request.Email,
            request.Phone,
            request.Source,
            ip);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/waitlist/{result.Value.Id}", result.Value)
            : result.Error.ToErrorResult();
    }
}
