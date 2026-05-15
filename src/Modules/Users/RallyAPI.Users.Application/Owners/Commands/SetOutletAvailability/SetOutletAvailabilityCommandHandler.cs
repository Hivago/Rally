using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Owners.Commands.SetOutletAvailability;

internal sealed class SetOutletAvailabilityCommandHandler
    : IRequestHandler<SetOutletAvailabilityCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetOutletAvailabilityCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        SetOutletAvailabilityCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(
            request.RestaurantId,
            cancellationToken);

        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        if (restaurant.OwnerId != request.OwnerId)
            return Result.Failure(Error.Forbidden("You do not have access to this outlet."));

        var result = request.IsAcceptingOrders
            ? restaurant.StartAcceptingOrders()
            : restaurant.StopAcceptingOrders();

        if (result.IsFailure)
            return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
