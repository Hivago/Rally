using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Owners.Commands.SetAllOutletsAvailability;

internal sealed class SetAllOutletsAvailabilityCommandHandler
    : IRequestHandler<SetAllOutletsAvailabilityCommand, Result<SetAllOutletsAvailabilityResponse>>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetAllOutletsAvailabilityCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SetAllOutletsAvailabilityResponse>> Handle(
        SetAllOutletsAvailabilityCommand request,
        CancellationToken cancellationToken)
    {
        var outlets = await _restaurantRepository.GetByOwnerIdAsync(
            request.OwnerId,
            cancellationToken);

        var updated = 0;
        var skipped = 0;

        foreach (var outlet in outlets)
        {
            var toggle = request.IsAcceptingOrders
                ? outlet.StartAcceptingOrders()
                : outlet.StopAcceptingOrders();

            if (toggle.IsSuccess)
            {
                _restaurantRepository.Update(outlet, cancellationToken);
                updated++;
            }
            else
            {
                skipped++;
            }
        }

        if (updated > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SetAllOutletsAvailabilityResponse(updated, skipped);
    }
}
