using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Infrastructure.BackgroundServices;

public sealed class RiderPayoutAggregationJob : BackgroundService
{
    private static readonly TimeZoneInfo IstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RiderPayoutAggregationJob> _logger;

    public RiderPayoutAggregationJob(
        IServiceProvider serviceProvider,
        ILogger<RiderPayoutAggregationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RiderPayoutAggregationJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                DateTime nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstTimeZone);

                if (nowIst.DayOfWeek == DayOfWeek.Monday && nowIst.Hour == 6)
                    await AggregateRiderPayoutsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rider payout aggregation failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task AggregateRiderPayoutsAsync(CancellationToken ct)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();

        var earnings = scope.ServiceProvider.GetRequiredService<IDeliveryEarningsQueryService>();
        var payoutRepository = scope.ServiceProvider.GetRequiredService<IRiderPayoutLedgerRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<RiderPayoutDispatchOptions>>();

        (DateTimeOffset cycleStart, DateTimeOffset cycleEnd) = GetPreviousWeeklyCycle(DateTimeOffset.UtcNow);
        IReadOnlyList<RiderEarningsSummary> summaries =
            await earnings.GetEarningsByCycleAsync(cycleStart, cycleEnd, ct);

        decimal percentage = options.Value.RiderEarningsPercentage;
        int updatedCount = 0;

        foreach (RiderEarningsSummary summary in summaries)
        {
            decimal baseFare = Math.Round(summary.TotalDeliveryFee * (percentage / 100m), 2);
            decimal surgeFare = 0m; // placeholder until surge pricing ships
            decimal tips = 0m; // placeholder until tipping ships

            RiderPayoutLedger? existing = await payoutRepository.GetByCycleAsync(
                summary.RiderId,
                cycleStart.UtcDateTime,
                cycleEnd.UtcDateTime,
                ct);

            if (existing is null)
            {
                var payout = RiderPayoutLedger.Create(
                    summary.RiderId,
                    cycleStart.UtcDateTime,
                    cycleEnd.UtcDateTime,
                    summary.DeliveryCount,
                    baseFare,
                    surgeFare,
                    tips);

                await payoutRepository.AddAsync(payout, ct);
                updatedCount++;
                continue;
            }

            if (existing.Status != RiderPayoutStatus.Pending)
                continue;

            existing.UpdateAmounts(summary.DeliveryCount, baseFare, surgeFare, tips);
            payoutRepository.Update(existing);
            updatedCount++;
        }

        await unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Rider payout aggregation complete: {Count} riders, cycle {CycleStart:yyyy-MM-dd} to {CycleEnd:yyyy-MM-dd}",
            updatedCount,
            cycleStart,
            cycleEnd);
    }

    private static (DateTimeOffset CycleStart, DateTimeOffset CycleEnd) GetPreviousWeeklyCycle(DateTimeOffset nowUtc)
    {
        int daysSinceMonday = ((int)nowUtc.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        DateTime thisMonday0030Utc = nowUtc.UtcDateTime.Date.AddDays(-daysSinceMonday).AddMinutes(30);
        var cycleEnd = new DateTimeOffset(thisMonday0030Utc, TimeSpan.Zero);

        if (nowUtc < cycleEnd)
            cycleEnd = cycleEnd.AddDays(-7);

        return (cycleEnd.AddDays(-7), cycleEnd);
    }
}

public sealed class RiderPayoutDispatchOptions
{
    public const string SectionName = "Dispatch";

    public decimal RiderEarningsPercentage { get; set; } = 80m;
}
