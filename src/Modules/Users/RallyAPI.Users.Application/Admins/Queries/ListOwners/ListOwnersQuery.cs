using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.ListOwners;

public sealed record ListOwnersQuery(
    Guid RequestedByAdminId,
    bool? IsActive,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<ListOwnersResponse>>;

public sealed record ListOwnersResponse(
    List<OwnerListItem> Owners,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record OwnerListItem(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string? PanNumber,
    string? GstNumber,
    bool IsActive,
    int OutletCount,
    DateTime CreatedAt);
