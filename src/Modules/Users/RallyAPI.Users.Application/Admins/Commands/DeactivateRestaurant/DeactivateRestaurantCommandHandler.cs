using MediatR;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.DeactivateRestaurant;

internal sealed class DeactivateRestaurantCommandHandler
    : IRequestHandler<DeactivateRestaurantCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateRestaurantCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        DeactivateRestaurantCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);

        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        var result = restaurant.Deactivate();

        if (result.IsFailure)
            return result;

        _restaurantRepository.Update(restaurant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
