using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.GetAdminOrderDetail;

internal sealed class GetAdminOrderDetailQueryHandler
    : IRequestHandler<GetAdminOrderDetailQuery, Result<AdminOrderDetail>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IAdminOrderQueryService _orders;

    public GetAdminOrderDetailQueryHandler(
        IAdminRepository adminRepository,
        IAdminOrderQueryService orders)
    {
        _adminRepository = adminRepository;
        _orders = orders;
    }

    public async Task<Result<AdminOrderDetail>> Handle(
        GetAdminOrderDetailQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByIdAsync(request.RequestedByAdminId, cancellationToken);
        if (admin is null)
            return Result.Failure<AdminOrderDetail>(Error.NotFound("Admin", request.RequestedByAdminId));

        var detail = await _orders.GetDetailAsync(request.OrderId, cancellationToken);
        if (detail is null)
            return Result.Failure<AdminOrderDetail>(Error.NotFound("Order", request.OrderId));

        return Result.Success(detail);
    }
}
