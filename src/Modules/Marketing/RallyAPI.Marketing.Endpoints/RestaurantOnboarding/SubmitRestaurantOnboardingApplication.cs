using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Marketing.Application.RestaurantOnboarding.Commands.SubmitApplication;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Marketing.Endpoints.RestaurantOnboarding;

/// <summary>
/// Public onboarding-form endpoint — served alongside the standalone static page
/// (wwwroot/partner-with-us.html). Creates a Pending application only; no live account is
/// created here. See docs/restaurant-onboarding-application-notes.md for the full design.
/// </summary>
public sealed class SubmitRestaurantOnboardingApplication : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/restaurant-onboarding", HandleAsync)
            .WithName("SubmitRestaurantOnboardingApplication")
            .WithTags("Marketing")
            .WithSummary("Public onboarding form: submit a restaurant's details for admin review.")
            .AllowAnonymous()
            .RequireRateLimiting("restaurant-onboarding");
    }

    public sealed record SubmitRestaurantOnboardingApplicationRequest(
        string RestaurantName,
        string OwnerName,
        string Phone,
        string Email,
        string City,
        string AddressLine,
        string? CuisineType,
        string? FssaiNumber,
        string BankAccountNumber,
        string BankIfscCode,
        string BankAccountName,
        string PanNumber,
        string? GstNumber,
        string? Source,
        // Honeypot: a field real users never see or fill (hidden via CSS on the form).
        // Any bot that blindly fills every input trips it. Non-empty here → pretend success,
        // never process the submission, never tell the bot it was caught.
        string? Website);

    private static async Task<IResult> HandleAsync(
        SubmitRestaurantOnboardingApplicationRequest request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
            return Results.Created($"/api/restaurant-onboarding/{Guid.NewGuid()}", new { id = Guid.NewGuid() });

        var ip = httpContext.Connection.RemoteIpAddress?.ToString();

        var command = new SubmitRestaurantOnboardingApplicationCommand(
            request.RestaurantName,
            request.OwnerName,
            request.Phone,
            request.Email,
            request.City,
            request.AddressLine,
            request.CuisineType,
            request.FssaiNumber,
            request.BankAccountNumber,
            request.BankIfscCode,
            request.BankAccountName,
            request.PanNumber,
            request.GstNumber,
            request.Source,
            ip);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/restaurant-onboarding/{result.Value.Id}", result.Value)
            : result.Error.ToErrorResult();
    }
}
