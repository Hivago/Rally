using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.ListAdmins;

public sealed record ListAdminsQuery(
    Guid RequestedByAdminId,
    string? Role,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<ListAdminsResponse>>;

public sealed record ListAdminsResponse(
    List<AdminListItem> Admins,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminListItem(
    Guid Id,
    string Email,
    string Name,
    string Role,
    bool IsActive,
    DateTime CreatedAt);
