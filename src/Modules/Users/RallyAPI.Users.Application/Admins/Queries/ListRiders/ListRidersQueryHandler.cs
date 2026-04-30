using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Queries.ListRiders;

internal sealed class ListRidersQueryHandler
    : IRequestHandler<ListRidersQuery, Result<ListRidersResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRiderRepository _riderRepository;

    public ListRidersQueryHandler(
        IAdminRepository adminRepository,
        IRiderRepository riderRepository)
    {
        _adminRepository = adminRepository;
        _riderRepository = riderRepository;
    }

    public async Task<Result<ListRidersResponse>> Handle(
        ListRidersQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<ListRidersResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        KycStatus? kycStatus = null;
        if (request.KycStatus is not null)
        {
            if (!Enum.TryParse<KycStatus>(request.KycStatus, ignoreCase: true, out var parsed))
                return Result.Failure<ListRidersResponse>(Error.Validation("Invalid KycStatus value."));
            kycStatus = parsed;
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (riders, totalCount) = await _riderRepository.GetPagedAsync(
            request.IsOnline, kycStatus, page, pageSize, cancellationToken);

        var riderItems = riders.Select(r => new RiderListItem(
            r.Id,
            r.Phone.GetFormatted(),
            r.Name,
            r.VehicleType.ToString(),
            r.KycStatus.ToString(),
            r.IsActive,
            r.IsOnline)).ToList();

        return new ListRidersResponse(riderItems, totalCount, page, pageSize);
    }
}
