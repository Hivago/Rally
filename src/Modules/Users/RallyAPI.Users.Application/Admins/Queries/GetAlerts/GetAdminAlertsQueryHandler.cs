using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetAlerts;

internal sealed class GetAdminAlertsQueryHandler
    : IRequestHandler<GetAdminAlertsQuery, Result<AdminAlertsResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IAdminAlertsService _alerts;

    public GetAdminAlertsQueryHandler(
        IAdminRepository adminRepository,
        IAdminAlertsService alerts)
    {
        _adminRepository = adminRepository;
        _alerts = alerts;
    }

    public async Task<Result<AdminAlertsResponse>> Handle(
        GetAdminAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<AdminAlertsResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        var alerts = await _alerts.GetActiveAlertsAsync(request.Limit, cancellationToken);
        return Result.Success(new AdminAlertsResponse(alerts.Count, alerts));
    }
}
