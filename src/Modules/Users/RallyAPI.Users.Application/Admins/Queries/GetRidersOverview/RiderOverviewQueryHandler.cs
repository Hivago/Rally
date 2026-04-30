using MediatR;
using Microsoft.EntityFrameworkCore;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Domain.Errors;
using RallyAPI.Persistence; // adjust to your DbContext namespace

namespace RallyAPI.Users.Application.Queries.RiderOverview;

public class RiderOverviewQueryHandler
    : IRequestHandler<RiderOverviewQuery, Result<RiderOverviewResponse>>
{
    private readonly IAppDbContext _dbContext;

    public RiderOverviewQueryHandler(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<RiderOverviewResponse>> Handle(
        RiderOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var rider = await _dbContext.Riders
            .AsNoTracking()
            .Where(r => r.Id == request.RiderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (rider is null)
            return Result<RiderOverviewResponse>.Failure(UserErrors.RiderNotFound);

        var now = DateTime.UtcNow;
        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
        var startOfMonth = new DateTime(now.Year, now.Month, 1);

        // Aggregate order stats
        var orders = _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.RiderId == request.RiderId);

        var totalDeliveries = await orders.CountAsync(cancellationToken);
        var completedDeliveries = await orders.CountAsync(o => o.Status == OrderStatus.Completed, cancellationToken);
        var cancelledDeliveries = await orders.CountAsync(o => o.Status == OrderStatus.Cancelled, cancellationToken);
        var ongoingDeliveries = await orders.CountAsync(o =>
            o.Status == OrderStatus.Assigned ||
            o.Status == OrderStatus.PickedUp ||
            o.Status == OrderStatus.InTransit, cancellationToken);

        // Ratings
        var ratingsQuery = _dbContext.Ratings
            .AsNoTracking()
            .Where(r => r.RiderId == request.RiderId);

        var totalRatings = await ratingsQuery.CountAsync(cancellationToken);
        var averageRating = totalRatings > 0
            ? await ratingsQuery.AverageAsync(r => (decimal)r.Score, cancellationToken)
            : 0m;

        // Earnings
        var earningsQuery = _dbContext.RiderEarnings
            .AsNoTracking()
            .Where(e => e.RiderId == request.RiderId);

        var totalEarnings = await earningsQuery.SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
        var pendingEarnings = await earningsQuery
            .Where(e => !e.IsPaidOut)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
        var earningsThisWeek = await earningsQuery
            .Where(e => e.CreatedAt >= startOfWeek)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
        var earningsThisMonth = await earningsQuery
            .Where(e => e.CreatedAt >= startOfMonth)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var response = new RiderOverviewResponse(
            RiderId: rider.Id,
            FullName: $"{rider.FirstName} {rider.LastName}".Trim(),
            Email: rider.Email,
            PhoneNumber: rider.PhoneNumber,
            Status: rider.Status.ToString(),
            IsVerified: rider.IsVerified,
            IsActive: rider.IsActive,
            JoinedAt: rider.CreatedAt,
            VehicleType: rider.Vehicle?.Type,
            VehiclePlateNumber: rider.Vehicle?.PlateNumber,

            TotalDeliveries: totalDeliveries,
            CompletedDeliveries: completedDeliveries,
            CancelledDeliveries: cancelledDeliveries,
            OngoingDeliveries: ongoingDeliveries,
            AverageRating: Math.Round(averageRating, 2),
            TotalRatings: totalRatings,

            TotalEarnings: totalEarnings,
            PendingEarnings: pendingEarnings,
            EarningsThisWeek: earningsThisWeek,
            EarningsThisMonth: earningsThisMonth,

            LastKnownLatitude: rider.LastKnownLocation?.Latitude,
            LastKnownLongitude: rider.LastKnownLocation?.Longitude,
            LastActiveAt: rider.LastActiveAt
        );

        return Result<RiderOverviewResponse>.Success(response);
    }
}