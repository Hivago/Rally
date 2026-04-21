using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateDietary;

internal sealed class UpdateRestaurantDietaryCommandHandler
    : IRequestHandler<UpdateRestaurantDietaryCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRestaurantDietaryCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateRestaurantDietaryCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        if (request.DietaryType.HasValue)
        {
            var dtResult = restaurant.SetDietaryType(request.DietaryType.Value);
            if (dtResult.IsFailure) return dtResult;
        }

        if (request.IsVeganFriendly.HasValue || request.HasJainOptions.HasValue)
        {
            var flagsResult = restaurant.SetDietaryAttributes(
                restaurant.IsPureVeg,
                request.IsVeganFriendly ?? restaurant.IsVeganFriendly,
                request.HasJainOptions ?? restaurant.HasJainOptions);
            if (flagsResult.IsFailure) return flagsResult;
        }

        if (request.CuisineTypes is not null)
        {
            var cuisineResult = restaurant.SetCuisineTypes(request.CuisineTypes);
            if (cuisineResult.IsFailure) return cuisineResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
