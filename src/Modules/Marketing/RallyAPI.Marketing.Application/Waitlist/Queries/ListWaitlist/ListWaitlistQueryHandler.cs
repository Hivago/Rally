using MediatR;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.Waitlist.Queries.ListWaitlist;

internal sealed class ListWaitlistQueryHandler
    : IRequestHandler<ListWaitlistQuery, Result<ListWaitlistResponse>>
{
    private readonly ICustomerWaitlistRepository _repository;

    public ListWaitlistQueryHandler(ICustomerWaitlistRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<ListWaitlistResponse>> Handle(
        ListWaitlistQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, total) = await _repository.GetPagedAsync(
            request.Search,
            page,
            pageSize,
            cancellationToken);

        var mapped = items
            .Select(e => new WaitlistEntryListItem(
                e.Id,
                e.Name,
                e.Email,
                e.Phone,
                e.Source,
                e.CreatedAt))
            .ToList();

        return Result.Success(new ListWaitlistResponse(mapped, total, page, pageSize));
    }
}
