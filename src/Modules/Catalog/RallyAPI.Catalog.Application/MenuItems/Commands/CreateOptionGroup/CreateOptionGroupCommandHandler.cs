using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.Enums;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.CreateOptionGroup;

internal sealed class CreateOptionGroupCommandHandler
    : IRequestHandler<CreateOptionGroupCommand, Result<CreateOptionGroupResponse>>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOptionGroupCommandHandler(
        IMenuItemRepository menuItemRepository,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateOptionGroupResponse>> Handle(
        CreateOptionGroupCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _menuItemRepository.GetByIdWithOptionsAsync(request.MenuItemId, cancellationToken);

        if (menuItem is null)
            return Result.Failure<CreateOptionGroupResponse>(MenuItemErrors.NotFound);

        if (menuItem.RestaurantId != request.RestaurantId)
            return Result.Failure<CreateOptionGroupResponse>(MenuItemErrors.Unauthorized);

        var group = MenuItemOptionGroup.Create(
            menuItem.Id,
            request.GroupName,
            request.IsRequired,
            request.MinSelections,
            request.MaxSelections,
            request.DisplayOrder);

        if (request.Options?.Any() == true)
        {
            foreach (var optionDto in request.Options)
            {
                var optionType = Enum.Parse<OptionType>(optionDto.Type, ignoreCase: true);
                var option = MenuItemOption.Create(
                    menuItem.Id,
                    optionDto.Name,
                    optionType,
                    optionDto.AdditionalPrice,
                    optionDto.IsDefault,
                    group.Id);

                group.AddOption(option);
                menuItem.AddOption(option);
            }
        }

        menuItem.AddOptionGroup(group);

        _menuItemRepository.Update(menuItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOptionGroupResponse(group.Id);
    }
}
