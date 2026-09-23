using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Commands.ApproveRestaurantLocationCorrection;

internal sealed class ApproveRestaurantLocationCorrectionCommandHandler
    : IRequestHandler<ApproveRestaurantLocationCorrectionCommand, Result>
{
    private readonly IRestaurantLocationReviewQueueRepository _reviewQueueRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveRestaurantLocationCorrectionCommandHandler(
        IRestaurantLocationReviewQueueRepository reviewQueueRepository,
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _reviewQueueRepository = reviewQueueRepository;
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ApproveRestaurantLocationCorrectionCommand request,
        CancellationToken cancellationToken)
    {
        var queueEntry = await _reviewQueueRepository.GetByIdAsync(request.QueueEntryId, cancellationToken);
        if (queueEntry is null)
            return Result.Failure(Error.NotFound("RestaurantLocationReviewQueue", request.QueueEntryId));

        var restaurant = await _restaurantRepository.GetByIdAsync(queueEntry.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", queueEntry.RestaurantId));

        var correctionResult = restaurant.ApplyLocationCorrection(
            queueEntry.SuggestedLatitude,
            queueEntry.SuggestedLongitude);
        if (correctionResult.IsFailure)
            return correctionResult;

        var approveResult = queueEntry.Approve(request.AdminId, DateTimeOffset.UtcNow);
        if (approveResult.IsFailure)
            return approveResult;

        _restaurantRepository.Update(restaurant, cancellationToken);
        _reviewQueueRepository.Update(queueEntry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
