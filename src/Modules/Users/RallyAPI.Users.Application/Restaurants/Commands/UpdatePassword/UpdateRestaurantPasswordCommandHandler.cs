using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdatePassword;

internal sealed class UpdateRestaurantPasswordCommandHandler
    : IRequestHandler<UpdateRestaurantPasswordCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRestaurantPasswordCommandHandler(
        IRestaurantRepository restaurantRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateRestaurantPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        if (!_passwordHasher.Verify(request.CurrentPassword, restaurant.PasswordHash))
            return Result.Failure(Error.Validation("Current password is incorrect."));

        var newHash = _passwordHasher.Hash(request.NewPassword);
        var r = restaurant.UpdatePassword(newHash);
        if (r.IsFailure) return r;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
