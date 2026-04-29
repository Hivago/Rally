using MediatR;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Queries.GetAdminOrders;

public sealed record GetAdminOrdersQuery(
    Guid RequestedByAdminId,
    AdminOrdersTab Tab,
    string? Search,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize) : IRequest<Result<AdminOrdersPagedResult>>;
