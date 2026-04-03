// File: src/RallyAPI.SharedKernel/Abstractions/Restaurants/RestaurantDetails.cs

namespace RallyAPI.SharedKernel.Abstractions.Restaurants;

/// <summary>
/// Full restaurant info for detail views.
/// </summary>
public sealed record RestaurantDetails
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Phone { get; init; }
    public required string AddressLine { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsAcceptingOrders { get; init; }
    public required bool AutoAcceptOrders { get; init; }
    public required int AvgPrepTimeMins { get; init; }
    public required TimeOnly OpeningTime { get; init; }
    public required TimeOnly ClosingTime { get; init; }
    public required decimal CommissionPercentage { get; init; }
    public Guid? OwnerId { get; init; }
}
