// File: src/Modules/Users/RallyAPI.Users.Infrastructure/Services/RestaurantQueryService.cs
// Purpose: Implements IRestaurantQueryService — queries users.restaurants table.
//          Consumed by Catalog module for browse/search endpoints.

using Microsoft.EntityFrameworkCore;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.Users.Infrastructure.Persistence;

namespace RallyAPI.Users.Infrastructure.Services;

internal sealed class RestaurantQueryService : IRestaurantQueryService
{
    private readonly UsersDbContext _context;

    public RestaurantQueryService(UsersDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RestaurantSummary>> GetActiveRestaurantsAsync(
        double? latitude = null,
        double? longitude = null,
        double? radiusKm = null,
        CancellationToken ct = default)
    {
        var query = _context.Restaurants
            .Where(r => r.IsActive && r.DeletedAt == null);

        var restaurants = await query.ToListAsync(ct);

        var summaries = restaurants.Select(r =>
        {
            double? distanceKm = null;

            if (latitude.HasValue && longitude.HasValue)
            {
                distanceKm = HaversineDistance(
                    latitude.Value, longitude.Value,
                    (double)r.Latitude, (double)r.Longitude);
            }

            return new RestaurantSummary
            {
                Id = r.Id,
                Name = r.Name,
                AddressLine = r.AddressLine,
                Latitude = (double)r.Latitude,
                Longitude = (double)r.Longitude,
                IsAcceptingOrders = r.IsAcceptingOrders,
                AvgPrepTimeMins = r.AvgPrepTimeMins,
                OpeningTime = r.OpeningTime,
                ClosingTime = r.ClosingTime,
                CuisineTypes = r.CuisineTypes,
                IsPureVeg = r.IsPureVeg,
                IsVeganFriendly = r.IsVeganFriendly,
                HasJainOptions = r.HasJainOptions,
                MinOrderAmount = r.MinOrderAmount,
                LogoUrl = r.LogoUrl,
                DistanceKm = distanceKm.HasValue ? Math.Round(distanceKm.Value, 2) : null
            };
        }).ToList();

        // Filter by radius if location provided
        if (latitude.HasValue && longitude.HasValue && radiusKm.HasValue)
        {
            summaries = summaries
                .Where(r => r.DistanceKm <= radiusKm.Value)
                .ToList();
        }

        // Sort: by distance if location provided, otherwise by name
        summaries = latitude.HasValue
            ? summaries.OrderBy(r => r.DistanceKm).ToList()
            : summaries.OrderBy(r => r.Name).ToList();

        return summaries;
    }

    public async Task<RestaurantDetails?> GetByIdAsync(
        Guid restaurantId,
        CancellationToken ct = default)
    {
        var r = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.Id == restaurantId && r.IsActive && r.DeletedAt == null, ct);

        if (r is null) return null;

        return new RestaurantDetails
        {
            Id = r.Id,
            Name = r.Name,
            Phone = r.Phone.Value,
            AddressLine = r.AddressLine,
            Latitude = (double)r.Latitude,
            Longitude = (double)r.Longitude,
            IsActive = r.IsActive,
            IsAcceptingOrders = r.IsAcceptingOrders,
            AutoAcceptOrders = r.AutoAcceptOrders,
            AvgPrepTimeMins = r.AvgPrepTimeMins,
            OpeningTime = r.OpeningTime,
            ClosingTime = r.ClosingTime,
            CommissionPercentage = r.CommissionPercentage,
            OwnerId = r.OwnerId
        };
    }

    /// <summary>
    /// Haversine formula — calculates distance between two lat/lng points in km.
    /// Same formula used in RiderQueryService.
    /// </summary>
    private static double HaversineDistance(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        const double R = 6371.0; // Earth radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
