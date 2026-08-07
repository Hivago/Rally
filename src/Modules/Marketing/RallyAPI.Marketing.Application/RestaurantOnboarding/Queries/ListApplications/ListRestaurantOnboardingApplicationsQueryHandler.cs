using MediatR;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.RestaurantOnboarding.Queries.ListApplications;

public sealed class ListRestaurantOnboardingApplicationsQueryHandler
    : IRequestHandler<ListRestaurantOnboardingApplicationsQuery, Result<ListRestaurantOnboardingApplicationsResult>>
{
    private readonly IRestaurantOnboardingApplicationRepository _repository;

    public ListRestaurantOnboardingApplicationsQueryHandler(IRestaurantOnboardingApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<ListRestaurantOnboardingApplicationsResult>> Handle(
        ListRestaurantOnboardingApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Status, request.Search, request.Page, request.PageSize, cancellationToken);

        var dtos = items.Select(a => new RestaurantOnboardingApplicationSummaryDto(
            a.Id,
            a.RestaurantName,
            a.OwnerName,
            a.Phone,
            a.Email,
            a.City,
            a.Status,
            Mask(a.BankAccountLast4),
            Mask(a.PanLast4),
            a.GstLast4 is null ? null : Mask(a.GstLast4),
            a.CreatedAt)).ToList();

        return Result.Success(new ListRestaurantOnboardingApplicationsResult(
            dtos, total, request.Page, request.PageSize));
    }

    private static string Mask(string last4) => $"•••• {last4}";
}
