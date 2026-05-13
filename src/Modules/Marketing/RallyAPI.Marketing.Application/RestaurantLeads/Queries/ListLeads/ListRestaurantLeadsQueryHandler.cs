using MediatR;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.RestaurantLeads.Queries.ListLeads;

internal sealed class ListRestaurantLeadsQueryHandler
    : IRequestHandler<ListRestaurantLeadsQuery, Result<ListRestaurantLeadsResponse>>
{
    private readonly IRestaurantLeadRepository _repository;

    public ListRestaurantLeadsQueryHandler(IRestaurantLeadRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<ListRestaurantLeadsResponse>> Handle(
        ListRestaurantLeadsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, total) = await _repository.GetPagedAsync(
            request.Search,
            page,
            pageSize,
            cancellationToken);

        var filtered = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.City))
            filtered = filtered.Where(l =>
                l.City.Equals(request.City, StringComparison.OrdinalIgnoreCase));

        if (request.DailyOrders.HasValue)
            filtered = filtered.Where(l => l.DailyOrders == request.DailyOrders.Value);

        var mapped = filtered
            .Select(l => new RestaurantLeadListItem(
                l.Id,
                l.RestaurantName,
                l.OwnerName,
                l.Phone,
                l.City,
                l.DailyOrders,
                l.Source,
                l.CreatedAt))
            .ToList();

        return Result.Success(new ListRestaurantLeadsResponse(mapped, total, page, pageSize));
    }
}
