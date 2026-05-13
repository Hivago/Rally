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
            .AsNoTracking()
            .Where(r => r.IsActive && r.DeletedAt == null);

        var restaurants = await query.ToListAsync(ct);

        var summaries = restaurants.Select(r => ToSummary(r, latitude, longitude)).ToList();

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

    public async Task<PagedRestaurantList> BrowseAsync(
        RestaurantListFilter filter,
        CancellationToken ct = default)
    {
        // Defend the service even if endpoint layer forgets to clamp.
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => filter.PageSize
        };

        var query = _context.Restaurants
            .AsNoTracking()
            .Where(r => r.IsActive && r.DeletedAt == null);

        // SQL-side filters (cheap scalar comparisons)
        if (filter.PureVeg == true)
            query = query.Where(r => r.IsPureVeg);

        if (filter.VeganFriendly == true)
            query = query.Where(r => r.IsVeganFriendly);

        if (filter.JainOptions == true)
            query = query.Where(r => r.HasJainOptions);

        if (filter.OpenNow == true)
            query = query.Where(r => r.IsAcceptingOrders);

        if (filter.SupportsPickup.HasValue)
            query = query.Where(r => r.AcceptsPickup == filter.SupportsPickup.Value);

        if (filter.MaxPrepTimeMins.HasValue)
            query = query.Where(r => r.AvgPrepTimeMins <= filter.MaxPrepTimeMins.Value);

        if (filter.MinPrice.HasValue)
            query = query.Where(r => r.MinOrderAmount >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query = query.Where(r => r.MinOrderAmount <= filter.MaxPrice.Value);

        // Name keyword — ILIKE is the Npgsql case-insensitive operator. Cuisine keyword
        // would need to touch the jsonb column, which doesn't translate cleanly, so we
        // handle it in-memory after materialization.
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(r => EF.Functions.ILike(r.Name, pattern));
        }

        var rows = await query.ToListAsync(ct);

        IEnumerable<RestaurantSummary> result = rows.Select(r => ToSummary(r, filter.Latitude, filter.Longitude));

        // In-memory filters: cuisine list (jsonb) + Haversine radius
        if (filter.Cuisines is { Count: > 0 })
        {
            var wanted = filter.Cuisines;
            result = result.Where(r =>
                r.CuisineTypes.Any(c => wanted.Contains(c, StringComparer.OrdinalIgnoreCase)));
        }

        if (filter.Latitude.HasValue && filter.Longitude.HasValue && filter.RadiusKm.HasValue)
        {
            var radius = filter.RadiusKm.Value;
            result = result.Where(r => r.DistanceKm.HasValue && r.DistanceKm.Value <= radius);
        }

        // Sort
        result = (filter.Sort?.ToLowerInvariant()) switch
        {
            "distance" => result.OrderBy(r => r.DistanceKm ?? double.MaxValue),
            "cost_asc" => result.OrderBy(r => r.MinOrderAmount),
            "cost_desc" => result.OrderByDescending(r => r.MinOrderAmount),
            "prep_time" => result.OrderBy(r => r.AvgPrepTimeMins),
            "newest" => result.OrderByDescending(r => r.CreatedAt),
            "relevance" => SortByRelevance(result, filter.Search),
            _ => filter.Latitude.HasValue
                ? result.OrderBy(r => r.DistanceKm ?? double.MaxValue)
                : result.OrderBy(r => r.Name)
        };

        var materialized = result.ToList();
        var total = materialized.Count;

        var paged = materialized
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedRestaurantList(paged, total, page, pageSize);
    }

    private static IOrderedEnumerable<RestaurantSummary> SortByRelevance(
        IEnumerable<RestaurantSummary> items,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return items.OrderBy(r => r.Name);

        var term = search.Trim();
        return items
            .OrderByDescending(r => Score(r, term))
            .ThenBy(r => r.Name);

        static int Score(RestaurantSummary r, string term)
        {
            if (r.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase)) return 3;
            if (r.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) return 2;
            if (r.CuisineTypes.Any(c => c.Contains(term, StringComparison.OrdinalIgnoreCase))) return 1;
            return 0;
        }
    }

    private static RestaurantSummary ToSummary(
        Users.Domain.Entities.Restaurant r,
        double? lat,
        double? lng)
    {
        double? distanceKm = null;
        if (lat.HasValue && lng.HasValue)
        {
            distanceKm = HaversineDistance(
                lat.Value, lng.Value,
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
            AcceptsPickup = r.AcceptsPickup,
            CreatedAt = r.CreatedAt,
            DistanceKm = distanceKm.HasValue ? Math.Round(distanceKm.Value, 2) : null
        };
    }

    public async Task<RestaurantDetails?> GetByIdAsync(
        Guid restaurantId,
        CancellationToken ct = default)
    {
        var r = await _context.Restaurants
            .AsNoTracking()
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
            CommissionFlatFee = r.CommissionFlatFee,
            OwnerId = r.OwnerId,
            AcceptsPickup = r.AcceptsPickup
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

    public async Task<IReadOnlyDictionary<Guid, OwnerPayoutDisplay>> GetOwnerPayoutDisplaysAsync(
        IReadOnlyCollection<Guid> ownerIds,
        CancellationToken ct = default)
    {
        if (ownerIds.Count == 0)
            return new Dictionary<Guid, OwnerPayoutDisplay>();

        // Owner names come from users.restaurant_owners.
        var owners = await _context.RestaurantOwners
            .AsNoTracking()
            .Where(o => ownerIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Name })
            .ToListAsync(ct);

        // Outlet count + first restaurant name come from users.restaurants.
        var outletData = await _context.Restaurants
            .AsNoTracking()
            .Where(r => r.OwnerId.HasValue && ownerIds.Contains(r.OwnerId.Value))
            .GroupBy(r => r.OwnerId!.Value)
            .Select(g => new
            {
                OwnerId = g.Key,
                OutletCount = g.Count(),
                FirstName = g.OrderBy(r => r.Name).Select(r => r.Name).FirstOrDefault()
            })
            .ToListAsync(ct);

        var outletByOwner = outletData.ToDictionary(x => x.OwnerId);

        return owners.ToDictionary(
            o => o.Id,
            o =>
            {
                outletByOwner.TryGetValue(o.Id, out var info);
                return new OwnerPayoutDisplay(
                    o.Id,
                    o.Name,
                    info?.OutletCount ?? 0,
                    info?.FirstName);
            });
    }
}
