using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Commands.ReleaseRiderDelivery;

internal sealed class ReleaseRiderDeliveryCommandHandler
    : IRequestHandler<ReleaseRiderDeliveryCommand, Result<ReleaseRiderDeliveryResult>>
{
    private readonly IRiderRepository _riderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReleaseRiderDeliveryCommandHandler> _logger;

    public ReleaseRiderDeliveryCommandHandler(
        IRiderRepository riderRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReleaseRiderDeliveryCommandHandler> logger)
    {
        _riderRepository = riderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ReleaseRiderDeliveryResult>> Handle(
        ReleaseRiderDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var rider = await _riderRepository.GetByIdAsync(request.RiderId, cancellationToken);
        if (rider is null)
            return Result.Failure<ReleaseRiderDeliveryResult>(Error.NotFound("Rider", request.RiderId));

        var previous = rider.CurrentDeliveryId;

        if (previous is null)
        {
            _logger.LogInformation("Rider {RiderId} had no active delivery to release", request.RiderId);
            return Result.Success(new ReleaseRiderDeliveryResult(rider.Id, null, false));
        }

        rider.ForceClearDelivery();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Admin force-released rider {RiderId} from delivery {DeliveryId}; rider is now available for offers",
            request.RiderId, previous);

        return Result.Success(new ReleaseRiderDeliveryResult(rider.Id, previous, true));
    }
}
