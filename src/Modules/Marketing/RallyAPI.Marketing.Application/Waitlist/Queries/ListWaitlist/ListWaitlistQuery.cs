using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.Waitlist.Queries.ListWaitlist;

public sealed record ListWaitlistQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<ListWaitlistResponse>>;

public sealed record ListWaitlistResponse(
    List<WaitlistEntryListItem> Entries,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record WaitlistEntryListItem(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string? Source,
    DateTime CreatedAt);
