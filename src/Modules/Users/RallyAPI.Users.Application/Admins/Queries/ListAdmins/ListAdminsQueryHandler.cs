using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Queries.ListAdmins;

internal sealed class ListAdminsQueryHandler
    : IRequestHandler<ListAdminsQuery, Result<ListAdminsResponse>>
{
    private readonly IAdminRepository _adminRepository;

    public ListAdminsQueryHandler(IAdminRepository adminRepository)
    {
        _adminRepository = adminRepository;
    }

    public async Task<Result<ListAdminsResponse>> Handle(
        ListAdminsQuery request,
        CancellationToken cancellationToken)
    {
        var requester = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (requester is null)
            return Result.Failure<ListAdminsResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        if (requester.Role != AdminRole.SuperAdmin)
            return Result.Failure<ListAdminsResponse>(
                Error.Forbidden("Only SuperAdmin can list admin users."));

        AdminRole? roleFilter = null;
        if (request.Role is not null)
        {
            if (!Enum.TryParse<AdminRole>(request.Role, ignoreCase: true, out var parsed))
                return Result.Failure<ListAdminsResponse>(Error.Validation("Invalid role value."));
            roleFilter = parsed;
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (admins, totalCount) = await _adminRepository.GetPagedAsync(
            roleFilter, request.IsActive, page, pageSize, cancellationToken);

        var items = admins.Select(a => new AdminListItem(
            a.Id,
            a.Email.Value,
            a.Name,
            a.Role.ToString(),
            a.IsActive,
            a.CreatedAt)).ToList();

        return new ListAdminsResponse(items, totalCount, page, pageSize);
    }
}
