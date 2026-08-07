using RallyAPI.Marketing.Domain.Entities;
using RallyAPI.Marketing.Domain.Enums;

namespace RallyAPI.Marketing.Application.Abstractions;

public interface IRestaurantOnboardingApplicationRepository
{
    Task AddAsync(RestaurantOnboardingApplication application, CancellationToken cancellationToken = default);

    void Update(RestaurantOnboardingApplication application);

    Task<RestaurantOnboardingApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True if a Pending application already exists for this phone or email — blocks a
    /// duplicate/spam resubmission while a prior submission is still awaiting review.
    /// </summary>
    Task<bool> HasPendingApplicationAsync(string phone, string email, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<RestaurantOnboardingApplication> Items, int Total)> GetPagedAsync(
        OnboardingApplicationStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
