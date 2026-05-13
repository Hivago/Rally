using RallyAPI.SharedKernel.Domain;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Domain.Entities;

public sealed class RestaurantLead : BaseEntity
{
    /// <summary>
    /// Allowed "avg daily orders" bucket caps. The number is the upper bound of each bucket
    /// (1000 acts as the sentinel for "500+"). Keep these in sync with the landing-page dropdown.
    /// </summary>
    public static readonly IReadOnlySet<int> AllowedDailyOrderBuckets = new HashSet<int> { 50, 200, 500, 1000 };

    public string RestaurantName { get; private set; } = string.Empty;
    public string OwnerName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public int DailyOrders { get; private set; }
    public string? Source { get; private set; }
    public string? IpAddress { get; private set; }

    private RestaurantLead() { }

    public static Result<RestaurantLead> Create(
        string restaurantName,
        string ownerName,
        string phone,
        string city,
        int dailyOrders,
        string? source = null,
        string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(restaurantName))
            return Result.Failure<RestaurantLead>(Error.Validation("Restaurant name is required."));

        if (string.IsNullOrWhiteSpace(ownerName))
            return Result.Failure<RestaurantLead>(Error.Validation("Owner name is required."));

        if (string.IsNullOrWhiteSpace(phone))
            return Result.Failure<RestaurantLead>(Error.Validation("Phone is required."));

        if (string.IsNullOrWhiteSpace(city))
            return Result.Failure<RestaurantLead>(Error.Validation("City is required."));

        if (!AllowedDailyOrderBuckets.Contains(dailyOrders))
            return Result.Failure<RestaurantLead>(
                Error.Validation($"Daily orders must be one of: {string.Join(", ", AllowedDailyOrderBuckets)}."));

        return Result.Success(new RestaurantLead
        {
            Id = Guid.NewGuid(),
            RestaurantName = restaurantName.Trim(),
            OwnerName = ownerName.Trim(),
            Phone = phone.Trim(),
            City = city.Trim(),
            DailyOrders = dailyOrders,
            Source = source?.Trim(),
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }
}
