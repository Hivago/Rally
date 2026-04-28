using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Commands.SetRestaurantStatus;

internal sealed class SetRestaurantStatusCommandHandler
    : IRequestHandler<SetRestaurantStatusCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetRestaurantStatusCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        SetRestaurantStatusCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);

        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        // Idempotent: same state requested means nothing to do.
        if (restaurant.IsActive == request.IsActive)
            return Result.Success();

        var result = request.IsActive
            ? restaurant.Activate()
            : restaurant.Deactivate();

        if (result.IsFailure)
            return result;

        _restaurantRepository.Update(restaurant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
