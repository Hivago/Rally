using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.ListOwners;

internal sealed class ListOwnersQueryHandler
    : IRequestHandler<ListOwnersQuery, Result<ListOwnersResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IRestaurantOwnerRepository _ownerRepository;
    private readonly IRestaurantRepository _restaurantRepository;

    public ListOwnersQueryHandler(
        IAdminRepository adminRepository,
        IRestaurantOwnerRepository ownerRepository,
        IRestaurantRepository restaurantRepository)
    {
        _adminRepository = adminRepository;
        _ownerRepository = ownerRepository;
        _restaurantRepository = restaurantRepository;
    }

    public async Task<Result<ListOwnersResponse>> Handle(
        ListOwnersQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<ListOwnersResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (owners, total) = await _ownerRepository.GetPagedAsync(
            request.IsActive,
            request.Search,
            page,
            pageSize,
            cancellationToken);

        var ownerIds = owners.Select(o => o.Id).ToList();
        var outletCounts = await _restaurantRepository.GetOutletCountsByOwnerIdsAsync(ownerIds, cancellationToken);

        var items = owners
            .Select(o => new OwnerListItem(
                o.Id,
                o.Name,
                o.Email.Value,
                o.Phone.Value,
                o.PanNumber,
                o.GstNumber,
                o.IsActive,
                outletCounts.TryGetValue(o.Id, out var count) ? count : 0,
                o.CreatedAt))
            .ToList();

        return Result.Success(new ListOwnersResponse(items, total, page, pageSize));
    }
}
