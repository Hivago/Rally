using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Commands.UpdateOption;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class UpdateOption : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/restaurant/options/{optionId:guid}", HandleAsync)
            .WithTags("Option Groups")
            .WithSummary("Update an option")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        Guid optionId,
        UpdateOptionRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateOptionCommand(
            restaurantId,
            optionId,
            request.Name,
            request.Type,
            request.AdditionalPrice,
            request.IsDefault);

        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}

public record UpdateOptionRequest(
    string Name,
    string Type,
    decimal AdditionalPrice,
    bool IsDefault);
