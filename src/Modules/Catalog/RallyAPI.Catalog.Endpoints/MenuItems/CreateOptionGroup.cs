using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Commands.CreateOptionGroup;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class CreateOptionGroup : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/restaurant/items/{menuItemId:guid}/option-groups", HandleAsync)
            .WithTags("Option Groups")
            .WithSummary("Create a new option group for a menu item")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        Guid menuItemId,
        CreateOptionGroupRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new CreateOptionGroupCommand(
            restaurantId,
            menuItemId,
            request.GroupName,
            request.IsRequired,
            request.MinSelections,
            request.MaxSelections,
            request.DisplayOrder,
            request.Options?.Select(o => new OptionItemDto(
                o.Name,
                o.Type,
                o.AdditionalPrice,
                o.IsDefault)).ToList());

        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Created($"/api/restaurant/items/{menuItemId}/option-groups/{result.Value.OptionGroupId}", result.Value)
            : result.Error.ToErrorResult();
    }
}

public record CreateOptionGroupRequest(
    string GroupName,
    bool IsRequired,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder,
    List<OptionItemRequest>? Options);

public record OptionItemRequest(
    string Name,
    string Type,
    decimal AdditionalPrice,
    bool IsDefault);
