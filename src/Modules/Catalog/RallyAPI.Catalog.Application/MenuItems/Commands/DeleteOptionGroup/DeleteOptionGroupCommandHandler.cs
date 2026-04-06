using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.DeleteOptionGroup;

internal sealed class DeleteOptionGroupCommandHandler
    : IRequestHandler<DeleteOptionGroupCommand, Result>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteOptionGroupCommandHandler(
        IMenuItemRepository menuItemRepository,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        DeleteOptionGroupCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _menuItemRepository.GetByIdWithOptionsAsync(request.MenuItemId, cancellationToken);

        if (menuItem is null)
            return Result.Failure(MenuItemErrors.NotFound);

        if (menuItem.RestaurantId != request.RestaurantId)
            return Result.Failure(MenuItemErrors.Unauthorized);

        var group = menuItem.OptionGroups.FirstOrDefault(g => g.Id == request.OptionGroupId);

        if (group is null)
            return Result.Failure(OptionGroupErrors.NotFound);

        // Remove all options belonging to this group from the menu item too
        foreach (var option in group.Options.ToList())
        {
            menuItem.RemoveOption(option.Id);
        }

        menuItem.RemoveOptionGroup(request.OptionGroupId);

        _menuItemRepository.Update(menuItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
