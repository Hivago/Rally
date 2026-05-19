using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.DeleteMenuItem;

internal sealed class DeleteMenuItemCommandHandler
    : IRequestHandler<DeleteMenuItemCommand, Result>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteMenuItemCommandHandler(
        IMenuItemRepository menuItemRepository,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        DeleteMenuItemCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _menuItemRepository.GetByIdAsync(request.MenuItemId, cancellationToken);

        if (menuItem is null)
            return Result.Failure(MenuItemErrors.NotFound);

        if (menuItem.RestaurantId != request.RestaurantId)
            return Result.Failure(MenuItemErrors.Unauthorized);

        _menuItemRepository.Delete(menuItem);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
