using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Infrastructure.BackgroundServices;

/// <summary>
/// Riders only go offline via an explicit GoOffline call from the app. If the app is killed,
/// crashes, or loses connectivity without a clean disconnect (SignalR OnDisconnectedAsync only
/// clears the connection tracker, not IsOnline), the rider stays flagged online forever.
/// This sweep periodically forces those riders offline so IsOnline stays trustworthy for
/// admin views, matching the freshness window dispatch already enforces separately
/// (RiderQueryService.MaxLocationAgeMinutes).
/// </summary>
public sealed class RiderPresenceSweepService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RiderPresenceSweepService> _logger;

    public RiderPresenceSweepService(IServiceScopeFactory scopeFactory, ILogger<RiderPresenceSweepService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RiderPresenceSweepService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepStaleRidersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rider presence sweep failed");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task SweepStaleRidersAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var riderRepository = scope.ServiceProvider.GetRequiredService<IRiderRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoff = DateTime.UtcNow - StaleAfter;
        var staleRiders = await riderRepository.GetStaleOnlineRidersAsync(cutoff, ct);

        if (staleRiders.Count == 0)
            return;

        foreach (var rider in staleRiders)
            rider.GoOffline();

        await unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Rider presence sweep: forced {Count} stale riders offline (no activity since {Cutoff:o})",
            staleRiders.Count,
            cutoff);
    }
}
