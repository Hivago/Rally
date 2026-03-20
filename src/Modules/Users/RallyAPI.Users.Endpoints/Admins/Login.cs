using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.Login;
using System;
using System.ComponentModel;
using System.Reflection.Metadata;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Model;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RallyAPI.Users.Endpoints.Admins;

public class Login : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admins/login", HandleAsync)
            .WithName("AdminLogin")
            .WithTags("Admins")
            .AllowAnonymous()
            .RequireRateLimiting("login");
    }

    public record AdminLoginRequest(string Email, string Password);

    private static async Task<IResult> HandleAsync(
        AdminLoginRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new LoginAdminCommand(request.Email, request.Password);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}

//HTTP Request → Endpoint (thin) → MediatR → Handler (thick) → Repository → DB
//Concern                      Who Handles It
//Route, HTTP method, tags     Endpoint(thin — just wiring)
//Request shape for API        Endpoint(the record inside)
//Business logic               Handler(password verify, JWT, refresh token)
//Validation                   Validator(FluentValidation, auto-triggered by MediatR pipeline)
//Data access                  Repository