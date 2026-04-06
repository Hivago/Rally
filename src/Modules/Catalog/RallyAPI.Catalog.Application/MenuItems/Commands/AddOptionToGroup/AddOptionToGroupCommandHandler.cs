using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.Enums;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.AddOptionToGroup;

internal sealed class AddOptionToGroupCommandHandler
    : IRequestHandler<AddOptionToGroupCommand, Result<AddOptionToGroupResponse>>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddOptionToGroupCommandHandler(
        IMenuItemRepository menuItemRepository,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AddOptionToGroupResponse>> Handle(
        AddOptionToGroupCommand request,
        CancellationToken cancellationToken)
    {
        // Find the menu item that owns this option group
        var menuItem = await _menuItemRepository.GetByOptionGroupIdAsync(request.OptionGroupId, cancellationToken);

        if (menuItem is null)
            return Result.Failure<AddOptionToGroupResponse>(OptionGroupErrors.NotFound);

        if (menuItem.RestaurantId != request.RestaurantId)
            return Result.Failure<AddOptionToGroupResponse>(MenuItemErrors.Unauthorized);

        var group = menuItem.OptionGroups.FirstOrDefault(g => g.Id == request.OptionGroupId);

        if (group is null)
            return Result.Failure<AddOptionToGroupResponse>(OptionGroupErrors.NotFound);

        var optionType = Enum.Parse<OptionType>(request.Type, ignoreCase: true);
        var option = MenuItemOption.Create(
            menuItem.Id,
            request.Name,
            optionType,
            request.AdditionalPrice,
            request.IsDefault,
            group.Id);

        group.AddOption(option);
        menuItem.AddOption(option);

        _menuItemRepository.Update(menuItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddOptionToGroupResponse(option.Id);
    }
}
