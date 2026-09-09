using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Commands.RejectRestaurantLocationCorrection;

internal sealed class RejectRestaurantLocationCorrectionCommandHandler
    : IRequestHandler<RejectRestaurantLocationCorrectionCommand, Result>
{
    private readonly IRestaurantLocationReviewQueueRepository _reviewQueueRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectRestaurantLocationCorrectionCommandHandler(
        IRestaurantLocationReviewQueueRepository reviewQueueRepository,
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _reviewQueueRepository = reviewQueueRepository;
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        RejectRestaurantLocationCorrectionCommand request,
        CancellationToken cancellationToken)
    {
        var queueEntry = await _reviewQueueRepository.GetByIdAsync(request.QueueEntryId, cancellationToken);
        if (queueEntry is null)
            return Result.Failure(Error.NotFound("RestaurantLocationReviewQueue", request.QueueEntryId));

        var restaurant = await _restaurantRepository.GetByIdAsync(queueEntry.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", queueEntry.RestaurantId));

        var rejectResult = queueEntry.Reject(request.AdminId, DateTimeOffset.UtcNow);
        if (rejectResult.IsFailure)
            return rejectResult;

        // Pin stays where it is — just dismiss the UnderReview flag so the
        // restaurant drops out of the "needs attention" list.
        restaurant.ClearLocationReview();

        _restaurantRepository.Update(restaurant, cancellationToken);
        _reviewQueueRepository.Update(queueEntry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
