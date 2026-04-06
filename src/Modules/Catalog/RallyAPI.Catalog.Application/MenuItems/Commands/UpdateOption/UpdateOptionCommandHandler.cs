using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.Enums;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.UpdateOption;

internal sealed class UpdateOptionCommandHandler
    : IRequestHandler<UpdateOptionCommand, Result>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOptionCommandHandler(
        IMenuItemRepository menuItemRepository,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateOptionCommand request,
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

        var optionType = Enum.Parse<OptionType>(request.Type, ignoreCase: true);
        option.Update(request.Name, optionType, request.AdditionalPrice, request.IsDefault);

        _menuItemRepository.Update(menuItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
