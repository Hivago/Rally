using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Owners.Commands.CancelTimeOff;

internal sealed class CancelTimeOffCommandHandler : IRequestHandler<CancelTimeOffCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantTimeOffRepository _timeOffRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelTimeOffCommandHandler(
        IRestaurantRepository restaurantRepository,
        IRestaurantTimeOffRepository timeOffRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _timeOffRepository = timeOffRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelTimeOffCommand request, CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        if (restaurant.OwnerId != request.OwnerId)
            return Result.Failure(Error.Forbidden("You do not have access to this outlet."));

        var timeOff = await _timeOffRepository.GetByIdAsync(request.TimeOffId, cancellationToken);
        if (timeOff is null || timeOff.RestaurantId != request.RestaurantId)
            return Result.Failure(Error.NotFound("TimeOff", request.TimeOffId));

        var cancelResult = timeOff.Cancel(DateTime.UtcNow);
        if (cancelResult.IsFailure)
            return cancelResult;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
