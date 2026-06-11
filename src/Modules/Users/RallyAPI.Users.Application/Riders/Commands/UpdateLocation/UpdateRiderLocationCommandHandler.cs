using MediatR;
using RallyAPI.SharedKernel.IntegrationEvents.Riders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Riders.Commands.UpdateLocation;

internal sealed class UpdateRiderLocationCommandHandler
    : IRequestHandler<UpdateRiderLocationCommand, Result>
{
    private readonly IRiderRepository _riderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public UpdateRiderLocationCommandHandler(
        IRiderRepository riderRepository,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _riderRepository = riderRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<Result> Handle(
        UpdateRiderLocationCommand request,
        CancellationToken cancellationToken)
    {
        var rider = await _riderRepository.GetByIdAsync(request.RiderId, cancellationToken);
        if (rider is null)
            return Result.Failure(Error.NotFound("Rider", request.RiderId));

        var result = rider.UpdateLocation(request.Latitude, request.Longitude);
        if (result.IsFailure)
            return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify the Delivery module so it can forward live position to the
        // customer if this rider is on an active own-fleet delivery. The
        // handler there no-ops when there is no active delivery.
        await _publisher.Publish(
            new RiderLocationUpdatedIntegrationEvent(
                request.RiderId,
                (double)request.Latitude,
                (double)request.Longitude,
                DateTime.UtcNow),
            cancellationToken);

        return Result.Success();
    }
}