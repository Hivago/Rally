using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetRiderPayouts;

internal sealed class GetRiderPayoutsQueryHandler
    : IRequestHandler<GetRiderPayoutsQuery, Result<RiderPayoutsPagedResult>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRiderPayoutQueryService _payouts;

    public GetRiderPayoutsQueryHandler(
        IAdminRepository adminRepository,
        IRiderPayoutQueryService payouts)
    {
        _adminRepository = adminRepository;
        _payouts = payouts;
    }

    public async Task<Result<RiderPayoutsPagedResult>> Handle(
        GetRiderPayoutsQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<RiderPayoutsPagedResult>(Error.NotFound("Admin", request.RequestedByAdminId));

        var filter = new RiderPayoutsFilter(
            request.FromUtc,
            request.ToUtc,
            request.RiderId,
            request.Status,
            request.Page,
            request.PageSize);

        RiderPayoutsPagedResult result = await _payouts.GetPayoutsAsync(filter, cancellationToken);
        return Result.Success(result);
    }
}
