using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.DeleteOption;

internal sealed class DeleteOptionCommandHandler
    : IRequestHandler<DeleteOptionCommand, Result>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteOptionCommandHandler(
        IMenuItemRepository menuItemRepository,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        DeleteOptionCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _menuItemRepository.GetByOptionIdAsync(request.OptionId, cancellationToken);

        if (menuItem is null)
            return Result.Failure(OptionGroupErrors.OptionNotFound);

        if (menuItem.RestaurantId != request.RestaurantId)
            return Result.Failure(MenuItemErrors.Unauthorized);

        var option = menuItem.Options.FirstOrDefault(o => o.Id == request.OptionId);

        if (option is null)
            return Result.Failure(OptionGroupErrors.OptionNotFound);

        // Also remove from the group if it belongs to one
        if (option.OptionGroupId.HasValue)
        {
            var group = menuItem.OptionGroups.FirstOrDefault(g => g.Id == option.OptionGroupId.Value);
            group?.RemoveOption(option.Id);
        }

        menuItem.RemoveOption(option.Id);

        _menuItemRepository.Update(menuItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
