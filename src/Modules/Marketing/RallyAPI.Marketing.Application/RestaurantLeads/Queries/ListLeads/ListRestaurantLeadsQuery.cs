using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.RestaurantLeads.Queries.ListLeads;

public sealed record ListRestaurantLeadsQuery(
    string? Search = null,
    string? City = null,
    int? DailyOrders = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<ListRestaurantLeadsResponse>>;

public sealed record ListRestaurantLeadsResponse(
    List<RestaurantLeadListItem> Leads,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record RestaurantLeadListItem(
    Guid Id,
    string RestaurantName,
    string OwnerName,
    string Phone,
    string City,
    int DailyOrders,
    string? Source,
    DateTime CreatedAt);
