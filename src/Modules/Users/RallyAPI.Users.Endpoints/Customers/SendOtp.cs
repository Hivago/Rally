using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Any;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Customers.Commands.SendOtp;

namespace RallyAPI.Users.Endpoints.Customers;

public class SendOtp : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/customers/otp/send", HandleAsync)
            .WithName("CustomerSendOtp")
            .WithTags("Customers")
            .AllowAnonymous()
            .RequireRateLimiting("otp")
            .WithOpenApi(operation =>
            {
                if (operation.RequestBody?.Content.TryGetValue("application/json", out var content) == true)
                {
                    content.Example = new OpenApiObject
                    {
                        ["phoneNumber"] = new OpenApiString("string")
                    };
                }

                return operation;
            });
    }

    public record SendCustomerOtpRequest(string PhoneNumber);

    private static async Task<IResult> HandleAsync(
        SendCustomerOtpRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new SendCustomerOtpCommand(request.PhoneNumber);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "OTP sent successfully" });
    }
}
