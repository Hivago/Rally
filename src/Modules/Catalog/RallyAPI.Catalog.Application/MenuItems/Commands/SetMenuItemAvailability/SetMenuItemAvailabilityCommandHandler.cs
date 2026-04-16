using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.SetMenuItemAvailability;

internal sealed class SetMenuItemAvailabilityCommandHandler
    : IRequestHandler<SetMenuItemAvailabilityCommand, Result<SetMenuItemAvailabilityResponse>>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetMenuItemAvailabilityCommandHandler(
        IMenuItemRepository menuItemRepository,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SetMenuItemAvailabilityResponse>> Handle(
        SetMenuItemAvailabilityCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _menuItemRepository.GetByIdAsync(
            request.MenuItemId,
            cancellationToken);

        if (menuItem is null)
            return Result.Failure<SetMenuItemAvailabilityResponse>(MenuItemErrors.NotFound);

        if (menuItem.RestaurantId != request.RestaurantId)
            return Result.Failure<SetMenuItemAvailabilityResponse>(MenuItemErrors.Unauthorized);

        menuItem.SetAvailability(request.IsAvailable);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SetMenuItemAvailabilityResponse(menuItem.IsAvailable);
    }
}
