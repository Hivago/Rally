using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateProfile;

internal sealed class UpdateRestaurantProfileCommandHandler
    : IRequestHandler<UpdateRestaurantProfileCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRestaurantProfileCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateRestaurantProfileCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(
            request.RestaurantId,
            cancellationToken);

        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        // Basic profile fields
        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var phoneResult = PhoneNumber.Create(request.Phone);
            if (phoneResult.IsFailure)
                return Result.Failure(phoneResult.Error);
            phone = phoneResult.Value;
        }

        var profileResult = restaurant.UpdateProfile(request.Name, request.AddressLine, phone);
        if (profileResult.IsFailure)
            return profileResult;

        // Cuisine types
        if (request.CuisineTypes is not null)
        {
            var cuisineResult = restaurant.SetCuisineTypes(request.CuisineTypes);
            if (cuisineResult.IsFailure)
                return cuisineResult;
        }

        // Dietary attributes (only update if any dietary field is provided)
        if (request.IsPureVeg.HasValue || request.IsVeganFriendly.HasValue || request.HasJainOptions.HasValue)
        {
            var dietaryResult = restaurant.SetDietaryAttributes(
                request.IsPureVeg ?? restaurant.IsPureVeg,
                request.IsVeganFriendly ?? restaurant.IsVeganFriendly,
                request.HasJainOptions ?? restaurant.HasJainOptions);
            if (dietaryResult.IsFailure)
                return dietaryResult;
        }

        // Min order amount
        if (request.MinOrderAmount.HasValue)
        {
            var minOrderResult = restaurant.SetMinOrderAmount(request.MinOrderAmount.Value);
            if (minOrderResult.IsFailure)
                return minOrderResult;
        }

        // FSSAI number
        if (!string.IsNullOrWhiteSpace(request.FssaiNumber))
        {
            var fssaiResult = restaurant.SetFssaiNumber(request.FssaiNumber);
            if (fssaiResult.IsFailure)
                return fssaiResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
