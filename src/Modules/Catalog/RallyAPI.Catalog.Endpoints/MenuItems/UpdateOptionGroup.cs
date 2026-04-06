using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Commands.UpdateOptionGroup;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class UpdateOptionGroup : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/restaurant/items/{menuItemId:guid}/option-groups/{groupId:guid}", HandleAsync)
            .WithTags("Option Groups")
            .WithSummary("Update an option group")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        Guid menuItemId,
        Guid groupId,
        UpdateOptionGroupRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateOptionGroupCommand(
            restaurantId,
            menuItemId,
            groupId,
            request.GroupName,
            request.IsRequired,
            request.MinSelections,
            request.MaxSelections,
            request.DisplayOrder);

        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}

public record UpdateOptionGroupRequest(
    string GroupName,
    bool IsRequired,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder);
