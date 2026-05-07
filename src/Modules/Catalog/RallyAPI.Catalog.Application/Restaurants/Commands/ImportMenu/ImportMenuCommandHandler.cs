using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.Enums;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.Catalog.Domain.Menus;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Storage;

namespace RallyAPI.Catalog.Application.Restaurants.Commands.ImportMenu;

internal sealed class ImportMenuCommandHandler
    : IRequestHandler<ImportMenuCommand, Result<ImportMenuResponse>>
{
    private readonly IMenuExcelParser _parser;
    private readonly IMenuRepository _menuRepository;
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStorageService _storage;
    private readonly ILogger<ImportMenuCommandHandler> _logger;

    public ImportMenuCommandHandler(
        IMenuExcelParser parser,
        IMenuRepository menuRepository,
        IMenuItemRepository menuItemRepository,
        IUnitOfWork unitOfWork,
        IStorageService storage,
        ILogger<ImportMenuCommandHandler> logger)
    {
        _parser = parser;
        _menuRepository = menuRepository;
        _menuItemRepository = menuItemRepository;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _logger = logger;
    }

    public async Task<Result<ImportMenuResponse>> Handle(
        ImportMenuCommand request,
        CancellationToken ct)
    {
        ParsedMenuWorkbook parsed;
        try
        {
            parsed = _parser.Parse(request.WorkbookStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse menu Excel workbook for restaurant {RestaurantId}", request.RestaurantId);
            return Result.Failure<ImportMenuResponse>(
                Error.Create("Catalog.Import.InvalidWorkbook", "The uploaded file could not be parsed as an Excel workbook."));
        }

        var validation = ValidateReferences(parsed, request.ImageBlobs);
        if (validation.Errors.Count > 0)
        {
            return Result.Failure<ImportMenuResponse>(
                Error.Create("Catalog.Import.ValidationFailed", string.Join(" | ", validation.Errors)));
        }

        var warnings = new List<string>(parsed.ParseErrors);
        warnings.AddRange(validation.Warnings);

        // Build aggregates and upload images.
        var importTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var menusByName = new Dictionary<string, Menu>(StringComparer.OrdinalIgnoreCase);
        foreach (var menuRow in parsed.Menus)
        {
            var menu = Menu.Create(request.RestaurantId, menuRow.Name, menuRow.Description, menuRow.DisplayOrder);
            menusByName[menuRow.Name] = menu;
            _menuRepository.Add(menu);
        }

        var itemsByName = new Dictionary<string, MenuItem>(StringComparer.OrdinalIgnoreCase);
        var imagesUploaded = 0;
        foreach (var itemRow in parsed.Items)
        {
            if (!menusByName.TryGetValue(itemRow.MenuName, out var menu))
            {
                warnings.Add($"Items row {itemRow.RowNumber}: menu '{itemRow.MenuName}' not found — item skipped.");
                continue;
            }

            string? imageUrl = null;
            if (!string.IsNullOrWhiteSpace(itemRow.ImageFilename) &&
                request.ImageBlobs.TryGetValue(itemRow.ImageFilename!, out var blob))
            {
                var ext = Path.GetExtension(itemRow.ImageFilename)?.TrimStart('.').ToLowerInvariant() ?? "jpg";
                var key = $"menu-items/{request.RestaurantId}/import-{importTimestamp}/{Guid.NewGuid():N}.{ext}";
                using var ms = new MemoryStream(blob.Content);
                imageUrl = await _storage.UploadAsync(ms, key, blob.ContentType, ct);
                imagesUploaded++;
            }

            var item = MenuItem.Create(
                menu.Id,
                request.RestaurantId,
                itemRow.ItemName,
                itemRow.Description,
                itemRow.BasePrice,
                imageUrl,
                itemRow.DisplayOrder,
                itemRow.IsVegetarian,
                itemRow.PreparationTimeMinutes);

            if (itemRow.Tags.Count > 0)
                item.SetTags(itemRow.Tags.ToList());

            itemsByName[itemRow.ItemName] = item;
            _menuItemRepository.Add(item);
        }

        var groupsByItemAndName = new Dictionary<(string ItemName, string GroupName), MenuItemOptionGroup>(
            new ItemGroupKeyComparer());
        var optionGroupsCreated = 0;
        foreach (var groupRow in parsed.OptionGroups)
        {
            if (!itemsByName.TryGetValue(groupRow.ItemName, out var item))
            {
                warnings.Add($"OptionGroups row {groupRow.RowNumber}: item '{groupRow.ItemName}' not found — group skipped.");
                continue;
            }

            var group = MenuItemOptionGroup.Create(
                item.Id,
                groupRow.GroupName,
                groupRow.IsRequired,
                groupRow.MinSelections,
                groupRow.MaxSelections,
                groupRow.DisplayOrder);

            item.AddOptionGroup(group);
            groupsByItemAndName[(groupRow.ItemName, groupRow.GroupName)] = group;
            optionGroupsCreated++;
        }

        var optionsCreated = 0;
        foreach (var optionRow in parsed.Options)
        {
            if (!itemsByName.TryGetValue(optionRow.ItemName, out var item))
            {
                warnings.Add($"Options row {optionRow.RowNumber}: item '{optionRow.ItemName}' not found — option skipped.");
                continue;
            }

            if (!Enum.TryParse<OptionType>(optionRow.OptionType, ignoreCase: true, out var optionType))
            {
                warnings.Add($"Options row {optionRow.RowNumber}: unknown OptionType '{optionRow.OptionType}' — defaulting to Choice.");
                optionType = OptionType.Choice;
            }

            Guid? groupId = null;
            if (groupsByItemAndName.TryGetValue((optionRow.ItemName, optionRow.GroupName), out var group))
                groupId = group.Id;

            var option = MenuItemOption.Create(
                item.Id,
                optionRow.OptionName,
                optionType,
                optionRow.AdditionalPrice,
                optionRow.IsDefault,
                groupId);

            if (group is not null)
                group.AddOption(option);

            item.AddOption(option);
            optionsCreated++;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new ImportMenuResponse(
            MenusCreated: menusByName.Count,
            ItemsCreated: itemsByName.Count,
            OptionGroupsCreated: optionGroupsCreated,
            OptionsCreated: optionsCreated,
            ImagesUploaded: imagesUploaded,
            Warnings: warnings));
    }

    private static (List<string> Errors, List<string> Warnings) ValidateReferences(
        ParsedMenuWorkbook parsed,
        IReadOnlyDictionary<string, ImageBlob> imageBlobs)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (parsed.Menus.Count == 0)
            errors.Add("Workbook contains no menus.");

        var menuNames = new HashSet<string>(parsed.Menus.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var item in parsed.Items)
        {
            if (!menuNames.Contains(item.MenuName))
                errors.Add($"Items row {item.RowNumber}: MenuName '{item.MenuName}' not declared on Menus sheet.");

            if (!string.IsNullOrWhiteSpace(item.ImageFilename) && !imageBlobs.ContainsKey(item.ImageFilename))
                warnings.Add($"Items row {item.RowNumber}: image '{item.ImageFilename}' not found in zip — item will be created without image.");
        }

        var itemNames = new HashSet<string>(parsed.Items.Select(i => i.ItemName), StringComparer.OrdinalIgnoreCase);
        foreach (var group in parsed.OptionGroups)
        {
            if (!itemNames.Contains(group.ItemName))
                errors.Add($"OptionGroups row {group.RowNumber}: ItemName '{group.ItemName}' not declared on Items sheet.");
        }

        foreach (var option in parsed.Options)
        {
            if (!itemNames.Contains(option.ItemName))
                errors.Add($"Options row {option.RowNumber}: ItemName '{option.ItemName}' not declared on Items sheet.");
        }

        return (errors, warnings);
    }

    private sealed class ItemGroupKeyComparer : IEqualityComparer<(string ItemName, string GroupName)>
    {
        public bool Equals((string ItemName, string GroupName) x, (string ItemName, string GroupName) y) =>
            string.Equals(x.ItemName, y.ItemName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.GroupName, y.GroupName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string ItemName, string GroupName) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ItemName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.GroupName));
    }
}
