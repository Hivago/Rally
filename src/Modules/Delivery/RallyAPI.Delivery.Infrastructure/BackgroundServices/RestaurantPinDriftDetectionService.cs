// File: src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/BackgroundServices/RestaurantPinDriftDetectionService.cs
// Purpose: Detect restaurants whose stored pin no longer matches where own-fleet
//   riders actually arrive, using the last N successful pickups' GPS telemetry.
// Pattern: IHostedService with PeriodicTimer (mirrors OrderAutoCancelService)

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Delivery.Infrastructure.Persistence;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;

namespace RallyAPI.Delivery.Infrastructure.BackgroundServices;

public sealed class RestaurantPinDriftDetectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RestaurantPinDriftDetectionService> _logger;
    private readonly RestaurantPinDriftOptions _options;

    public RestaurantPinDriftDetectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<RestaurantPinDriftDetectionService> logger,
        IOptions<RestaurantPinDriftOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RestaurantPinDriftDetectionService started. Interval: {IntervalSeconds}s | " +
            "Sample size: {SampleSize} | Threshold: {ThresholdMeters}m",
            _options.CheckIntervalSeconds, _options.SampleSize, _options.ThresholdMeters);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.CheckIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunSweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RestaurantPinDriftDetectionService cycle");
            }
        }
    }

    private async Task RunSweepAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var sampleSize = _options.SampleSize;

        // Restaurants with at least `sampleSize` own-fleet pickups carrying telemetry.
        // (RiderId != null excludes 3PL deliveries — their riders' GPS isn't ours to read.)
        var candidateRestaurantIds = await context.DeliveryRequests
            .AsNoTracking()
            .Where(d => d.RestaurantId != null
                        && d.RiderId != null
                        && d.PickedUpAt != null
                        && d.PickupDriftMeters != null)
            .GroupBy(d => d.RestaurantId)
            .Where(g => g.Count() >= sampleSize)
            .Select(g => g.Key!.Value)
            .ToListAsync(ct);

        if (candidateRestaurantIds.Count == 0)
            return;

        var flaggedCount = 0;

        foreach (var restaurantId in candidateRestaurantIds)
        {
            var samples = await context.DeliveryRequests
                .AsNoTracking()
                .Where(d => d.RestaurantId == restaurantId
                            && d.RiderId != null
                            && d.PickedUpAt != null
                            && d.PickupDriftMeters != null)
                .OrderByDescending(d => d.PickedUpAt)
                .Take(sampleSize)
                .Select(d => new
                {
                    d.PickupDriftMeters,
                    d.ArrivedPickupLatitude,
                    d.ArrivedPickupLongitude,
                    d.PickupLatitude,
                    d.PickupLongitude
                })
                .ToListAsync(ct);

            // "Consistently" drifted = every one of the last N samples exceeds the
            // threshold, not just the average — one bad GPS fix shouldn't trigger a
            // false flag, but it also shouldn't mask a real relocation.
            if (samples.Count < sampleSize || !samples.All(s => s.PickupDriftMeters!.Value > _options.ThresholdMeters))
                continue;

            var averageDrift = samples.Average(s => s.PickupDriftMeters!.Value);

            // Auto-correction: mathematical center point (simple mean) of the arrival
            // coordinates. At this scale (samples within ~tens to low-hundreds of
            // meters of each other) a flat centroid is accurate enough — no need for
            // proper geodesic clustering (k-means/DBSCAN) over 10 nearby points.
            var suggestedLatitude = samples.Average(s => s.ArrivedPickupLatitude!.Value);
            var suggestedLongitude = samples.Average(s => s.ArrivedPickupLongitude!.Value);

            await publisher.Publish(new RestaurantLocationDriftDetectedIntegrationEvent(
                restaurantId,
                samples[0].PickupLatitude,
                samples[0].PickupLongitude,
                suggestedLatitude,
                suggestedLongitude,
                averageDrift,
                samples.Count,
                DateTimeOffset.UtcNow), ct);

            flaggedCount++;

            _logger.LogWarning(
                "Restaurant {RestaurantId} pin drift detected: avg {AvgDrift:F1}m over {SampleSize} pickups. " +
                "Stored ({StoredLat},{StoredLng}) vs suggested ({SuggestedLat},{SuggestedLng})",
                restaurantId, averageDrift, samples.Count,
                samples[0].PickupLatitude, samples[0].PickupLongitude,
                suggestedLatitude, suggestedLongitude);
        }

        if (flaggedCount > 0)
            _logger.LogInformation("Pin drift sweep complete. Flagged: {FlaggedCount}", flaggedCount);
    }
}
