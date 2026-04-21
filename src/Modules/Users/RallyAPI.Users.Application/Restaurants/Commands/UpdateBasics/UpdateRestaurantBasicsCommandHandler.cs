using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateBasics;

internal sealed class UpdateRestaurantBasicsCommandHandler
    : IRequestHandler<UpdateRestaurantBasicsCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRestaurantBasicsCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateRestaurantBasicsCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var phoneResult = PhoneNumber.Create(request.Phone);
            if (phoneResult.IsFailure) return Result.Failure(phoneResult.Error);
            phone = phoneResult.Value;
        }

        var profileResult = restaurant.UpdateProfile(request.Name, request.AddressLine, phone);
        if (profileResult.IsFailure) return profileResult;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailResult = Email.Create(request.Email);
            if (emailResult.IsFailure) return Result.Failure(emailResult.Error);

            if (!restaurant.Email.Equals(emailResult.Value))
            {
                var exists = await _restaurantRepository.ExistsByEmailAsync(emailResult.Value, cancellationToken);
                if (exists) return Result.Failure(Error.Validation("Email is already in use by another restaurant."));
                var setEmailResult = restaurant.SetEmail(emailResult.Value);
                if (setEmailResult.IsFailure) return setEmailResult;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.FssaiNumber))
        {
            var fssaiResult = restaurant.SetFssaiNumber(request.FssaiNumber);
            if (fssaiResult.IsFailure) return fssaiResult;
        }

        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            var locResult = restaurant.UpdateLocation(request.Latitude.Value, request.Longitude.Value);
            if (locResult.IsFailure) return locResult;
        }

        if (request.Description is not null)
        {
            var descResult = restaurant.SetDescription(request.Description);
            if (descResult.IsFailure) return descResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
