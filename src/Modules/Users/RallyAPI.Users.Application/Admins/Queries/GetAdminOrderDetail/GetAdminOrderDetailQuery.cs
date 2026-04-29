using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetAdminOrderDetail;

public sealed record GetAdminOrderDetailQuery(
    Guid RequestedByAdminId,
    Guid OrderId) : IRequest<Result<AdminOrderDetail>>;
