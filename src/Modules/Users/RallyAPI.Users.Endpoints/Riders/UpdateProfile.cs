using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Riders.Commands.UpdateProfile;
using RallyAPI.Users.Application.Riders.Commands.UploadKyc;
using RallyAPI.Users.Domain.Entities;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Riders;

public class UpdateProfile : IEndpoint
{

    public sealed record GenerateKycUploadUrlRequest(
        string ContentType,
        RiderKycDocumentType DocumentType);

    public sealed record ConfirmKycRequest(
        string FileKey,
        RiderKycDocumentType DocumentType);


    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/riders/profile", HandleAsync)
            .WithTags("Riders")
            .WithSummary("Update rider profile")
            .RequireAuthorization("Rider");

        app.MapPost("api/users/riders/{riderId:guid}/kyc/upload-url",
    async (
        Guid riderId,
        [FromBody] GenerateKycUploadUrlRequest request,
        ISender sender,
        HttpContext httpContext) =>
    {
        var userType = httpContext.User.FindFirst("user_type")?.Value ?? "";
        var isAdmin = userType.Equals("admin", StringComparison.OrdinalIgnoreCase);
        var sub = Guid.TryParse(httpContext.User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

        var result = await sender.Send(new GenerateRiderKycUploadUrlCommand(
            riderId, sub, isAdmin, request.DocumentType, request.ContentType));

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    })
    .RequireAuthorization("RiderOrAdmin")
    .WithName("GenerateRiderKycUploadUrl")
    .WithTags("Users - Images");

        // PATCH /api/users/riders/{riderId}/kyc/confirm  (Step 2)
        app.MapPatch("api/users/riders/{riderId:guid}/kyc/confirm",
            async (
                Guid riderId,
                [FromBody] ConfirmKycRequest request,
                ISender sender,
                HttpContext httpContext) =>
            {
                var userType = httpContext.User.FindFirst("user_type")?.Value ?? "";
                var isAdmin = userType.Equals("admin", StringComparison.OrdinalIgnoreCase);
                var sub = Guid.TryParse(httpContext.User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

                var result = await sender.Send(new ConfirmRiderKycDocumentCommand(
                    riderId, sub, isAdmin, request.DocumentType, request.FileKey));
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.Error.ToErrorResult();
            })
            .RequireAuthorization("RiderOrAdmin")
            .WithName("ConfirmRiderKycDocument")
            .WithTags("Users - Images");
    }

    private static async Task<IResult> HandleAsync(
        UpdateRiderProfileRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var riderId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var command = new UpdateRiderProfileCommand(
            riderId,
            request.Name,
            request.Email,
            request.VehicleNumber);

        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new { message = "Profile updated successfully" })
            : result.Error.ToErrorResult();
    }
}

public record UpdateRiderProfileRequest(string? Name, string? Email, string? VehicleNumber);