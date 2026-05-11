using MediatR;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.ValueObjects;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.EditRestaurant;

internal sealed class EditRestaurantCommandHandler
    : IRequestHandler<EditRestaurantCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public EditRestaurantCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        EditRestaurantCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);

        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        if (request.Name is not null || request.AddressLine is not null || request.Phone is not null)
        {
            PhoneNumber? phone = null;
            if (request.Phone is not null)
            {
                var phoneResult = PhoneNumber.Create(request.Phone);
                if (phoneResult.IsFailure)
                    return Result.Failure(phoneResult.Error);
                phone = phoneResult.Value;
            }

            restaurant.UpdateProfile(
                request.Name,
                request.AddressLine,
                phone);
        }

        // Deprecated: percentage commission no longer supported. Only flat fee is used.
        // if (request.CommissionPercentage.HasValue)
        // {
        //     var commissionResult = restaurant.SetCommissionPercentage(request.CommissionPercentage.Value);
        //     if (commissionResult.IsFailure)
        //         return commissionResult;
        // }

        if (request.CommissionFlatFee.HasValue)
        {
            var flatFeeResult = restaurant.SetCommissionFlatFee(request.CommissionFlatFee.Value);
            if (flatFeeResult.IsFailure)
                return flatFeeResult;
        }

        if (request.AvgPrepTimeMins.HasValue)
            restaurant.SetPrepTime(request.AvgPrepTimeMins.Value);

        if (request.CuisineTypes is not null)
            restaurant.SetCuisineTypes(request.CuisineTypes);

        if (request.IsPureVeg.HasValue || request.IsVeganFriendly.HasValue || request.HasJainOptions.HasValue)
        {
            restaurant.SetDietaryAttributes(
                request.IsPureVeg ?? restaurant.IsPureVeg,
                request.IsVeganFriendly ?? restaurant.IsVeganFriendly,
                request.HasJainOptions ?? restaurant.HasJainOptions);
        }

        if (request.MinOrderAmount.HasValue)
            restaurant.SetMinOrderAmount(request.MinOrderAmount.Value);

        if (request.FssaiNumber is not null)
            restaurant.SetFssaiNumber(request.FssaiNumber);

        _restaurantRepository.Update(restaurant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
