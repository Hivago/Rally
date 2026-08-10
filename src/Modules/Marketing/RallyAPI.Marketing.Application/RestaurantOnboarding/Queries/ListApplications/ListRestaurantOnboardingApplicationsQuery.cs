using MediatR;
using RallyAPI.Marketing.Domain.Enums;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.RestaurantOnboarding.Queries.ListApplications;

/// <summary>
/// Admin review list. Sensitive fields are always masked here — full decryption only ever
/// happens in GetRestaurantOnboardingApplicationDetailQuery, and only when the caller has
/// been verified as Super Admin at the endpoint layer.
/// </summary>
public sealed record ListRestaurantOnboardingApplicationsQuery(
    OnboardingApplicationStatus? Status,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<ListRestaurantOnboardingApplicationsResult>>;

public sealed record ListRestaurantOnboardingApplicationsResult(
    IReadOnlyList<RestaurantOnboardingApplicationSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record RestaurantOnboardingApplicationSummaryDto(
    Guid Id,
    string RestaurantName,
    string OwnerName,
    string Phone,
    string Email,
    string City,
    OnboardingApplicationStatus Status,
    string? BankAccountMasked,   // null if the applicant didn't provide bank details
    string PanMasked,
    string? GstMasked,
    DateTime CreatedAt);
