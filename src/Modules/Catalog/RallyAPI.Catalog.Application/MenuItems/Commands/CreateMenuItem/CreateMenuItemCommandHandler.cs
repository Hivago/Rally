using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.Enums;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.CreateMenuItem;

internal sealed class CreateMenuItemCommandHandler
    : IRequestHandler<CreateMenuItemCommand, Result<CreateMenuItemResponse>>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IMenuRepository _menuRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateMenuItemCommandHandler(
        IMenuItemRepository menuItemRepository,
        IMenuRepository menuRepository,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _menuRepository = menuRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateMenuItemResponse>> Handle(
        CreateMenuItemCommand request,
        CancellationToken cancellationToken)
    {
        // Validate menu exists and belongs to restaurant
        var menu = await _menuRepository.GetByIdAsync(request.MenuId, cancellationToken);

        if (menu is null)
            return Result.Failure<CreateMenuItemResponse>(MenuItemErrors.MenuNotFound);

        if (menu.RestaurantId != request.RestaurantId)
            return Result.Failure<CreateMenuItemResponse>(MenuItemErrors.Unauthorized);

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<CreateMenuItemResponse>(MenuItemErrors.NameRequired);

        if (request.BasePrice <= 0)
            return Result.Failure<CreateMenuItemResponse>(MenuItemErrors.InvalidPrice);

        var menuItem = MenuItem.Create(
            request.MenuId,
            request.RestaurantId,
            request.Name,
            request.Description,
            request.BasePrice,
            request.ImageUrl,
            request.DisplayOrder,
            request.IsVegetarian,
            request.PreparationTimeMinutes);

        // Add standalone options if provided (backward compatible)
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
                    optionDto.IsDefault);

                menuItem.AddOption(option);
            }
        }

        // Add option groups if provided
        if (request.OptionGroups?.Any() == true)
        {
            foreach (var groupDto in request.OptionGroups)
            {
                var group = MenuItemOptionGroup.Create(
                    menuItem.Id,
                    groupDto.GroupName,
                    groupDto.IsRequired,
                    groupDto.MinSelections,
                    groupDto.MaxSelections,
                    groupDto.DisplayOrder);

                foreach (var optionDto in groupDto.Options)
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

                menuItem.AddOptionGroup(group);
            }
        }

        // Set tags if provided
        if (request.Tags is not null)
        {
            menuItem.SetTags(request.Tags);
        }

        _menuItemRepository.Add(menuItem);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateMenuItemResponse(menuItem.Id);
    }
}