using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.UpdateOptionGroup;

internal sealed class UpdateOptionGroupCommandHandler
    : IRequestHandler<UpdateOptionGroupCommand, Result>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOptionGroupCommandHandler(
        IMenuItemRepository menuItemRepository,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateOptionGroupCommand request,
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

        group.Update(
            request.GroupName,
            request.IsRequired,
            request.MinSelections,
            request.MaxSelections,
            request.DisplayOrder);

        _menuItemRepository.Update(menuItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
