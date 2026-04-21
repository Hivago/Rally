using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateDelivery;

internal sealed class UpdateRestaurantDeliveryCommandHandler
    : IRequestHandler<UpdateRestaurantDeliveryCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRestaurantDeliveryCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateRestaurantDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        var r = restaurant.SetDeliveryMode(request.DeliveryMode);
        if (r.IsFailure) return r;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
