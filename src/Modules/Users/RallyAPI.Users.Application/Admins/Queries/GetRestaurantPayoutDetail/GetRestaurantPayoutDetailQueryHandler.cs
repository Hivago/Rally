using MediatR;
using RallyAPI.SharedKernel.Abstractions.Payouts;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetRestaurantPayoutDetail;

internal sealed class GetRestaurantPayoutDetailQueryHandler
    : IRequestHandler<GetRestaurantPayoutDetailQuery, Result<AdminPayoutDetail>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IAdminPayoutQueryService _payouts;

    public GetRestaurantPayoutDetailQueryHandler(
        IAdminRepository adminRepository,
        IAdminPayoutQueryService payouts)
    {
        _adminRepository = adminRepository;
        _payouts = payouts;
    }

    public async Task<Result<AdminPayoutDetail>> Handle(
        GetRestaurantPayoutDetailQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<AdminPayoutDetail>(Error.NotFound("Admin", request.RequestedByAdminId));

        var detail = await _payouts.GetRestaurantPayoutDetailAsync(request.PayoutId, cancellationToken);
        if (detail is null)
            return Result.Failure<AdminPayoutDetail>(Error.NotFound("Payout", request.PayoutId));

        return Result.Success(detail);
    }
}
