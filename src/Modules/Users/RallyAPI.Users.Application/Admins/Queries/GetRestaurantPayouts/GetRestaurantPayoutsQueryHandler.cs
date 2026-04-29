using MediatR;
using RallyAPI.SharedKernel.Abstractions.Payouts;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetRestaurantPayouts;

internal sealed class GetRestaurantPayoutsQueryHandler
    : IRequestHandler<GetRestaurantPayoutsQuery, Result<RestaurantPayoutsPagedResult>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IAdminPayoutQueryService _payouts;

    public GetRestaurantPayoutsQueryHandler(
        IAdminRepository adminRepository,
        IAdminPayoutQueryService payouts)
    {
        _adminRepository = adminRepository;
        _payouts = payouts;
    }

    public async Task<Result<RestaurantPayoutsPagedResult>> Handle(
        GetRestaurantPayoutsQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<RestaurantPayoutsPagedResult>(Error.NotFound("Admin", request.RequestedByAdminId));

        var filter = new RestaurantPayoutsFilter(
            request.FromUtc,
            request.ToUtc,
            request.OwnerId,
            request.Status,
            request.Page,
            request.PageSize);

        var result = await _payouts.GetRestaurantPayoutsAsync(filter, cancellationToken);
        return Result.Success(result);
    }
}
