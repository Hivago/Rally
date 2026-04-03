using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Commands.CreateMenuItem;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class CreateMenuItem : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/restaurant/items", HandleAsync)
            .WithTags("Restaurant Menu Items")
            .WithSummary("Create a new menu item")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        CreateMenuItemRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new CreateMenuItemCommand(
            restaurantId,
            request.MenuId,
            request.Name,
            request.Description,
            request.BasePrice,
            request.ImageUrl,
            request.DisplayOrder,
            request.IsVegetarian,
            request.PreparationTimeMinutes,
            request.Options?.Select(o => new MenuItemOptionDto(
                o.Name,
                o.Type,
                o.AdditionalPrice,
                o.IsDefault)).ToList(),
            request.OptionGroups?.Select(g => new OptionGroupDto(
                g.GroupName,
                g.IsRequired,
                g.MinSelections,
                g.MaxSelections,
                g.DisplayOrder,
                g.Options.Select(o => new MenuItemOptionDto(
                    o.Name,
                    o.Type,
                    o.AdditionalPrice,
                    o.IsDefault)).ToList())).ToList(),
            request.Tags);

        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Created($"/api/items/{result.Value.MenuItemId}", result.Value)
            : result.Error.ToErrorResult();
    }
}

public record CreateMenuItemRequest(
    Guid MenuId,
    string Name,
    string? Description,
    decimal BasePrice,
    string? ImageUrl,
    int DisplayOrder,
    bool IsVegetarian,
    int PreparationTimeMinutes,
    List<MenuItemOptionRequest>? Options,
    List<OptionGroupRequest>? OptionGroups,
    List<string>? Tags);

public record MenuItemOptionRequest(
    string Name,
    string Type,
    decimal AdditionalPrice,
    bool IsDefault);

public record OptionGroupRequest(
    string GroupName,
    bool IsRequired,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder,
    List<MenuItemOptionRequest> Options);