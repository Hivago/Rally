using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Application.Abstractions;

public interface IOnboardingNotificationService
{
    Task NotifyNewApplicationAsync(RestaurantOnboardingApplication application, CancellationToken cancellationToken = default);
}
