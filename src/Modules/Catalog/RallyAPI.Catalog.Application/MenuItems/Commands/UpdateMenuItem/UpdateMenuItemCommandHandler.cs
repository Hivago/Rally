using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.Enums;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.UpdateMenuItem;

internal sealed class UpdateMenuItemCommandHandler
    : IRequestHandler<UpdateMenuItemCommand, Result>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateMenuItemCommandHandler(
        IMenuItemRepository menuItemRepository,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateMenuItemCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _menuItemRepository.GetByIdWithOptionsAsync(
            request.MenuItemId,
            cancellationToken);

        if (menuItem is null)
            return Result.Failure(MenuItemErrors.NotFound);

        if (menuItem.RestaurantId != request.RestaurantId)
            return Result.Failure(MenuItemErrors.Unauthorized);

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure(MenuItemErrors.NameRequired);

        if (request.BasePrice <= 0)
            return Result.Failure(MenuItemErrors.InvalidPrice);

        menuItem.Update(
            request.Name,
            request.Description,
            request.BasePrice,
            request.ImageUrl,
            request.DisplayOrder,
            request.IsVegetarian,
            request.PreparationTimeMinutes);

        // Update options - clear and re-add
        menuItem.ClearOptions();
        menuItem.ClearOptionGroups();

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

        // Update tags if provided
        if (request.Tags is not null)
        {
            menuItem.SetTags(request.Tags);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}