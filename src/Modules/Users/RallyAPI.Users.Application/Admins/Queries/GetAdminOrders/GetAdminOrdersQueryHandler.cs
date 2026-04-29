using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetAdminOrders;

internal sealed class GetAdminOrdersQueryHandler
    : IRequestHandler<GetAdminOrdersQuery, Result<AdminOrdersPagedResult>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IAdminOrderQueryService _orders;

    public GetAdminOrdersQueryHandler(
        IAdminRepository adminRepository,
        IAdminOrderQueryService orders)
    {
        _adminRepository = adminRepository;
        _orders = orders;
    }

    public async Task<Result<AdminOrdersPagedResult>> Handle(
        GetAdminOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<AdminOrdersPagedResult>(Error.NotFound("Admin", request.RequestedByAdminId));

        var filter = new AdminOrdersFilter(
            request.Tab,
            request.Search,
            request.FromUtc,
            request.ToUtc,
            request.Page,
            request.PageSize);

        var result = await _orders.SearchAsync(filter, cancellationToken);
        return Result.Success(result);
    }
}
