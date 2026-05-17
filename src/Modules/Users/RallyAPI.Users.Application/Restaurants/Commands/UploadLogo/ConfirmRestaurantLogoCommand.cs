using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Storage;
using RallyAPI.Users.Application.Abstractions;
using System.ComponentModel;

namespace RallyAPI.Users.Application.Restaurants.Commands.UploadLogo;

// ──────────────────────────────────────────────
// Command
// ──────────────────────────────────────────────

public sealed record ConfirmRestaurantLogoCommand(
    Guid RestaurantId,
    Guid RequestingRestaurantId,
    bool IsAdmin,
    string FileKey
) : IRequest<Result<ConfirmRestaurantLogoResponse>>;

public sealed record ConfirmRestaurantLogoResponse(
    Guid RestaurantId,
    string LogoUrl
);

// ──────────────────────────────────────────────
// Handler
// ──────────────────────────────────────────────

public sealed class ConfirmRestaurantLogoCommandHandler
    : IRequestHandler<ConfirmRestaurantLogoCommand, Result<ConfirmRestaurantLogoResponse>>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IStorageService _storage;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmRestaurantLogoCommandHandler(
        IRestaurantRepository restaurantRepository,
        IStorageService storage,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _storage = storage;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConfirmRestaurantLogoResponse>> Handle(
        ConfirmRestaurantLogoCommand command,
        CancellationToken ct)
    {
        // 1. Ownership check
        if (!command.IsAdmin && command.RestaurantId != command.RequestingRestaurantId)
            return Result.Failure<ConfirmRestaurantLogoResponse>(
                Error.Forbidden("You can only update your own restaurant logo."));

        // 2. Load restaurant
        var restaurant = await _restaurantRepository.GetByIdAsync(command.RestaurantId, ct);
        if (restaurant is null)
            return Result.Failure<ConfirmRestaurantLogoResponse>(
                Error.NotFound("Restaurant", command.RestaurantId));

        // 3. Validate the fileKey belongs to this restaurant
        var expectedPrefix = $"restaurants/{command.RestaurantId}/logo/";
        if (!command.FileKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<ConfirmRestaurantLogoResponse>(
                StorageErrors.InvalidFileKey);

        // 4. Delete old logo from R2 using stored FileKey — no fragile URL parsing
        if (!string.IsNullOrEmpty(restaurant.LogoFileKey))
            await _storage.DeleteAsync(restaurant.LogoFileKey, ct);

        // 5. Build public CDN URL and update the domain entity
        var publicUrl = _storage.BuildPublicUrl(command.FileKey);
        restaurant.UpdateLogoUrl(publicUrl, command.FileKey);

        // 6. Persist
        _restaurantRepository.Update(restaurant, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new ConfirmRestaurantLogoResponse(
            restaurant.Id,
            publicUrl));
    }
}   