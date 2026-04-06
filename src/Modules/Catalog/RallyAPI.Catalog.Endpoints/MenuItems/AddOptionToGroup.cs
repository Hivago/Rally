using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Commands.AddOptionToGroup;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class AddOptionToGroup : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/restaurant/option-groups/{groupId:guid}/options", HandleAsync)
            .WithTags("Option Groups")
            .WithSummary("Add an option to an existing option group")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        Guid groupId,
        AddOptionToGroupRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new AddOptionToGroupCommand(
            restaurantId,
            groupId,
            request.Name,
            request.Type,
            request.AdditionalPrice,
            request.IsDefault);

        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Created($"/api/restaurant/options/{result.Value.OptionId}", result.Value)
            : result.Error.ToErrorResult();
    }
}

public record AddOptionToGroupRequest(
    string Name,
    string Type,
    decimal AdditionalPrice,
    bool IsDefault);
