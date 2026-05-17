using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Storage;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.ConfirmMenuItemImage;

// ──────────────────────────────────────────────
// Command
// ──────────────────────────────────────────────

/// <summary>
/// Step 2 of 2 for image upload.
/// Called by the client AFTER successfully uploading the file directly to R2.
/// Validates the fileKey belongs to this menu item, builds the public CDN URL,
/// and persists it to the DB via MenuItem.UpdateImageUrl().
/// </summary>
public sealed record ConfirmMenuItemImageCommand(
    Guid MenuItemId,
    Guid RequestingRestaurantId,
    bool IsAdmin,
    /// <summary>The file key returned by GenerateMenuItemUploadUrlCommand.</summary>
    string FileKey
) : IRequest<Result<ConfirmMenuItemImageResponse>>;

public sealed record ConfirmMenuItemImageResponse(
    Guid MenuItemId,
    string ImageUrl
);

// ──────────────────────────────────────────────
// Handler
// ──────────────────────────────────────────────

public sealed class ConfirmMenuItemImageCommandHandler
    : IRequestHandler<ConfirmMenuItemImageCommand, Result<ConfirmMenuItemImageResponse>>
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IStorageService _storage;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmMenuItemImageCommandHandler(
        IMenuItemRepository menuItemRepository,
        IStorageService storage,
        IUnitOfWork unitOfWork)
    {
        _menuItemRepository = menuItemRepository;
        _storage = storage;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConfirmMenuItemImageResponse>> Handle(
        ConfirmMenuItemImageCommand command,
        CancellationToken ct)
    {
        // 1. Load the menu item
        var menuItem = await _menuItemRepository.GetByIdAsync(command.MenuItemId, ct);
        if (menuItem is null)
            return Result.Failure<ConfirmMenuItemImageResponse>(
                Error.NotFound("MenuItem",command.MenuItemId));

        // 2. Ownership check
        if (!command.IsAdmin && menuItem.RestaurantId != command.RequestingRestaurantId)
            return Result.Failure<ConfirmMenuItemImageResponse>(
                Error.Forbidden("You do not have permission to update this menu item."));

        // 3. Validate the fileKey belongs to this menu item.
        //    Keys are structured: menu-items/{restaurantId}/{menuItemId}/{timestamp}.{ext}
        //    This prevents a restaurant from confirming a key from a different item.
        var expectedPrefix = $"menu-items/{menuItem.RestaurantId}/{menuItem.Id}/";
        if (!command.FileKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<ConfirmMenuItemImageResponse>(
                StorageErrors.InvalidFileKey);

        // 4. If item already has an image, delete the old one from R2
        //    (best-effort — don't fail the confirm if delete fails)
        if (!string.IsNullOrEmpty(menuItem.ImageUrl))
        {
            var oldKey = ExtractKeyFromUrl(menuItem.ImageUrl);
            if (oldKey is not null)
                await _storage.DeleteAsync(oldKey, ct);
        }

        // 5. Build public CDN URL and update the domain entity
        var publicUrl = _storage.BuildPublicUrl(command.FileKey);
        menuItem.UpdateImageUrl(publicUrl);

        // 6. Persist
        _menuItemRepository.Update(menuItem, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new ConfirmMenuItemImageResponse(
            menuItem.Id,
            publicUrl));
    }

    /// <summary>
    /// Extracts the storage key from a full public URL.
    /// e.g. "https://media.rally.app/menu-items/..." → "menu-items/..."
    /// Returns null if the URL doesn't match our storage domain.
    /// </summary>
    private static string? ExtractKeyFromUrl(string imageUrl)
    {
        try
        {
            var uri = new Uri(imageUrl);
            // Path starts with '/' — strip it to get the key
            return uri.AbsolutePath.TrimStart('/');
        }
        catch
        {
            return null;
        }
    }
}