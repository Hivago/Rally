using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Abstractions.Restaurants;

namespace RallyAPI.Orders.Infrastructure.BackgroundServices;

/// <summary>
/// Runs every hour, but only creates payout batches on Monday mornings (IST).
/// For each owner with pending ledger entries from the previous week,
/// creates a Payout record and assigns the ledger entries to it.
/// </summary>
public sealed class WeeklyPayoutBatchService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private static readonly TimeZoneInfo IstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeeklyPayoutBatchService> _logger;

    public WeeklyPayoutBatchService(
        IServiceScopeFactory scopeFactory,
        ILogger<WeeklyPayoutBatchService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WeeklyPayoutBatchService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);

            try
            {
                var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstTimeZone);

                // Only run on Mondays between 6:00-7:00 AM IST
                if (istNow.DayOfWeek == DayOfWeek.Monday && istNow.Hour == 6)
                {
                    await CreateWeeklyPayoutBatchesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during weekly payout batch creation");
            }
        }
    }

    private async Task CreateWeeklyPayoutBatchesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ledgerRepo = scope.ServiceProvider.GetRequiredService<IPayoutLedgerRepository>();
        var payoutRepo = scope.ServiceProvider.GetRequiredService<IPayoutRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Previous week: Monday 00:00 to Sunday 23:59:59 IST
        var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstTimeZone);
        var thisMondayIst = istNow.Date; // Today is Monday
        var lastMondayIst = thisMondayIst.AddDays(-7);
        var lastSundayIst = thisMondayIst.AddDays(-1);

        // Convert to UTC for DB queries
        var periodStartUtc = TimeZoneInfo.ConvertTimeToUtc(lastMondayIst, IstTimeZone);
        var periodEndUtc = TimeZoneInfo.ConvertTimeToUtc(thisMondayIst, IstTimeZone);

        var periodStart = DateOnly.FromDateTime(lastMondayIst);
        var periodEnd = DateOnly.FromDateTime(lastSundayIst);

        _logger.LogInformation(
            "Creating payout batches for period {Start} to {End}",
            periodStart, periodEnd);

        // Find all owners with pending entries in this period
        var ownerIds = await ledgerRepo.GetOwnerIdsWithPendingEntriesAsync(
            periodStartUtc, periodEndUtc, ct);

        if (ownerIds.Count == 0)
        {
            _logger.LogInformation("No pending ledger entries for period {Start}-{End}", periodStart, periodEnd);
            return;
        }

        var batchCount = 0;

        foreach (var ownerId in ownerIds)
        {
            // Check if payout already exists for this owner+period (idempotency)
            var existingPayout = await payoutRepo.GetCurrentPeriodPayoutAsync(
                ownerId, periodStart, periodEnd, ct);

            if (existingPayout is not null)
            {
                _logger.LogWarning(
                    "Payout already exists for owner {OwnerId} period {Start}-{End}, skipping",
                    ownerId, periodStart, periodEnd);
                continue;
            }

            var pendingEntries = await ledgerRepo.GetPendingByOwnerIdAsync(ownerId, ct);
            if (pendingEntries.Count == 0) continue;

            // Create the payout batch
            var payout = Payout.CreateFromLedger(
                ownerId, periodStart, periodEnd,
                pendingEntries,
                bankAccountNumber: null, // Will be filled from owner record when admin processes
                bankIfscCode: null);

            // Assign ledger entries to this payout
            foreach (var entry in pendingEntries)
            {
                entry.AssignToPayout(payout.Id);
            }

            await payoutRepo.AddAsync(payout, ct);
            await unitOfWork.SaveChangesAsync(ct);

            batchCount++;

            _logger.LogInformation(
                "Created payout batch for owner {OwnerId}: {OrderCount} orders, net={NetAmount}",
                ownerId, payout.OrderCount, payout.NetPayoutAmount);
        }

        _logger.LogInformation(
            "Weekly payout batch complete: {BatchCount} payouts created for period {Start}-{End}",
            batchCount, periodStart, periodEnd);
    }
}
