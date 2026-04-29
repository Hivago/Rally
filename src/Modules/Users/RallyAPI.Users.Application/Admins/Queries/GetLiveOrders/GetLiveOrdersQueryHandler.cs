using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetLiveOrders;

internal sealed class GetLiveOrdersQueryHandler
    : IRequestHandler<GetLiveOrdersQuery, Result<LiveOrdersResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly ILiveOrderFeedService _feed;

    public GetLiveOrdersQueryHandler(
        IAdminRepository adminRepository,
        ILiveOrderFeedService feed)
    {
        _adminRepository = adminRepository;
        _feed = feed;
    }

    public async Task<Result<LiveOrdersResponse>> Handle(
        GetLiveOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<LiveOrdersResponse>(Error.NotFound("Admin", request.RequestedByAdminId));

        var orders = await _feed.GetLiveOrdersAsync(request.Limit, cancellationToken);
        return Result.Success(new LiveOrdersResponse(orders.Count, orders));
    }
}
