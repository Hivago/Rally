using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateOperations;

internal sealed class UpdateRestaurantOperationsCommandHandler
    : IRequestHandler<UpdateRestaurantOperationsCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRestaurantOperationsCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateRestaurantOperationsCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        if (request.AutoAcceptOrders.HasValue)
        {
            var r = restaurant.SetAutoAcceptOrders(request.AutoAcceptOrders.Value);
            if (r.IsFailure) return r;
        }

        if (request.AvgPrepTimeMins.HasValue)
        {
            var r = restaurant.SetPrepTime(request.AvgPrepTimeMins.Value);
            if (r.IsFailure) return r;
        }

        if (request.MinOrderAmount.HasValue)
        {
            var r = restaurant.SetMinOrderAmount(request.MinOrderAmount.Value);
            if (r.IsFailure) return r;
        }

        if (request.UseCustomSchedule.HasValue)
        {
            var r = restaurant.SetUseCustomSchedule(request.UseCustomSchedule.Value);
            if (r.IsFailure) return r;
        }

        if (request.IsAcceptingOrders.HasValue)
        {
            var r = request.IsAcceptingOrders.Value
                ? restaurant.StartAcceptingOrders()
                : restaurant.StopAcceptingOrders();
            if (r.IsFailure) return r;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
