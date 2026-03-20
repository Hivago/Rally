using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.Abstractions.Distance;
using RallyAPI.SharedKernel.Abstractions.Notifications;
using RallyAPI.SharedKernel.Abstractions.Pricing;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.Results;
using Respawn;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace RallyAPI.Integration.Tests.Infrastructure;

/// <summary>
/// Shared WebApplicationFactory for all integration tests.
/// Spins up PostgreSQL + Redis via TestContainers, generates RSA keys,
/// and stubs external services (Google Maps, SignalR, Delivery pricing).
///
/// Env vars are set after containers start so they override appsettings files
/// (env vars have higher priority in .NET config chain).
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("rally_test")
        .WithUsername("rally")
        .WithPassword("rally_test_pw")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    /// <summary>RSA key pair generated once for the test session — shared with TestJwtHelper.</summary>
    internal readonly RSA Rsa = RSA.Create(2048);

    private Respawner? _respawner;
    private IConnectionMultiplexer? _redisConnection;

    public async Task InitializeAsync()
    {
        // Start containers
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        // Set environment variables AFTER containers are running so their connection
        // strings are available. These override appsettings.*.json files in .NET's
        // configuration chain (env vars have higher priority).
        var publicKeyPem = Rsa.ExportSubjectPublicKeyInfoPem();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT",             "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__Database",         _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis",            _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("JwtSettings__PublicKeyPem",           publicKeyPem);
        Environment.SetEnvironmentVariable("JwtSettings__PublicKeyPath",          "");
        Environment.SetEnvironmentVariable("JwtSettings__Issuer",                 TestJwtHelper.Issuer);
        Environment.SetEnvironmentVariable("JwtSettings__Audience",               TestJwtHelper.Audience);
        Environment.SetEnvironmentVariable("AutoCancel__EscalateAfterMinutes",    "9999");
        Environment.SetEnvironmentVariable("AutoCancel__CancelAfterMinutes",      "9999");
        Environment.SetEnvironmentVariable("PayU__MerchantKey",                   "test_key");
        Environment.SetEnvironmentVariable("PayU__MerchantSalt",                  "test_salt");
        Environment.SetEnvironmentVariable("PayU__BaseUrl",                       "https://test.payu.invalid");
        Environment.SetEnvironmentVariable("Delivery__OwnFleet__AcceptanceTimeoutSeconds", "1");
        Environment.SetEnvironmentVariable("Delivery__OwnFleet__MaxRidersToTry",           "1");
        Environment.SetEnvironmentVariable("GoogleMaps__ApiKey",                  "test_key");

        // Keep a connection for Redis flush during test resets
        _redisConnection = ConnectionMultiplexer.Connect(
            new ConfigurationOptions
            {
                EndPoints = { _redis.GetConnectionString() },
                AllowAdmin = true
            });
    }

    public new async Task DisposeAsync()
    {
        // Clear the env vars we set so they don't leak into other test runs
        Environment.SetEnvironmentVariable("ConnectionStrings__Database",  null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis",     null);
        Environment.SetEnvironmentVariable("JwtSettings__PublicKeyPem",    null);
        Environment.SetEnvironmentVariable("JwtSettings__PublicKeyPath",   null);
        Environment.SetEnvironmentVariable("JwtSettings__Issuer",          null);
        Environment.SetEnvironmentVariable("JwtSettings__Audience",        null);

        _redisConnection?.Dispose();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        Rsa.Dispose();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace IConnectionMultiplexer with test Redis
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(_redis.GetConnectionString()));

            // Stub IRiderNotificationService — no real SignalR hubs in tests
            services.RemoveAll<IRiderNotificationService>();
            services.AddScoped<IRiderNotificationService, NoOpRiderNotificationService>();

            // Stub IDistanceCalculator — avoid real Google Maps API calls
            services.RemoveAll<IDistanceCalculator>();
            services.AddScoped<IDistanceCalculator, StubDistanceCalculator>();

            // Stub IRiderQueryService — always reports own fleet available (avoids 3PL fallback)
            services.RemoveAll<IRiderQueryService>();
            services.AddScoped<IRiderQueryService, StubRiderQueryService>();

            // Stub IDeliveryPricingCalculator — returns fixed INR 30 fee (avoids Pricing DB queries)
            services.RemoveAll<IDeliveryPricingCalculator>();
            services.AddScoped<IDeliveryPricingCalculator, StubDeliveryPricingCalculator>();
        });
    }

    /// <summary>
    /// Initialises Respawn after the first CreateClient() call (migrations have run).
    /// Call once per test collection before the first test.
    /// </summary>
    public async Task InitialiseRespawnerAsync()
    {
        if (_respawner is not null) return;

        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter        = DbAdapter.Postgres,
            SchemasToInclude = ["orders", "users", "catalog", "delivery", "pricing"]
        });
    }

    /// <summary>Wipes all module tables and flushes Redis so each test starts with a clean state.</summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
            throw new InvalidOperationException("Call InitialiseRespawnerAsync first.");

        // Reset PostgreSQL
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);

        // Flush Redis so cached carts/OTPs from previous tests don't bleed through
        if (_redisConnection is not null)
        {
            try
            {
                foreach (var endpoint in _redisConnection.GetEndPoints())
                {
                    var server = _redisConnection.GetServer(endpoint);
                    await server.FlushAllDatabasesAsync();
                }
            }
            catch
            {
                // Redis flush failure is non-fatal — tests may see stale cache but won't corrupt DB state
            }
        }
    }

    // ─── Test stubs ──────────────────────────────────────────────────────────────

    private sealed class NoOpRiderNotificationService : IRiderNotificationService
    {
        public Task<Result> SendDeliveryOfferAsync(
            Guid riderId, DeliveryOfferNotification offer, CancellationToken ct = default)
            => Task.FromResult(Result.Success());

        public Task<Result> SendOfferCancelledAsync(
            Guid riderId, Guid offerId, string reason, CancellationToken ct = default)
            => Task.FromResult(Result.Success());

        public Task<Result> SendNotificationAsync(
            Guid riderId, string title, string message, CancellationToken ct = default)
            => Task.FromResult(Result.Success());

        public Task<bool> IsRiderConnectedAsync(Guid riderId, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private sealed class StubDistanceCalculator : IDistanceCalculator
    {
        public Task<DistanceResult> GetDistanceAsync(
            double originLat, double originLng,
            double destinationLat, double destinationLng,
            CancellationToken ct = default)
            => Task.FromResult(DistanceResult.Success(5000, 900, "5 km", "15 mins"));

        public Task<IReadOnlyList<DistanceResult>> GetDistancesAsync(
            double originLat, double originLng,
            IReadOnlyList<(double lat, double lng)> destinations,
            CancellationToken ct = default)
        {
            IReadOnlyList<DistanceResult> results = destinations
                .Select(_ => DistanceResult.Success(5000, 900, "5 km", "15 mins"))
                .ToList();
            return Task.FromResult(results);
        }
    }

    private sealed class StubRiderQueryService : IRiderQueryService
    {
        public Task<IReadOnlyList<AvailableRider>> GetAvailableRidersAsync(
            double latitude, double longitude, double radiusKm, int maxResults = 10,
            CancellationToken ct = default)
        {
            IReadOnlyList<AvailableRider> results = Array.Empty<AvailableRider>();
            return Task.FromResult(results);
        }

        public Task<bool> IsOwnFleetAvailableAsync(
            double latitude, double longitude, double radiusKm,
            CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<RiderDetails?> GetRiderByIdAsync(Guid riderId, CancellationToken ct = default)
            => Task.FromResult<RiderDetails?>(null);

        public Task<RiderPublicInfo?> GetRiderPublicInfoAsync(Guid riderId, CancellationToken ct = default)
            => Task.FromResult<RiderPublicInfo?>(null);
    }

    private sealed class StubDeliveryPricingCalculator : IDeliveryPricingCalculator
    {
        public Task<DeliveryPriceResult> CalculateAsync(
            DeliveryPriceRequest request, CancellationToken ct = default)
        {
            var result = DeliveryPriceResult.Success(
                quoteId:          Guid.NewGuid().ToString(),
                baseFee:          30m,
                finalFee:         30m,
                distanceKm:       5m,
                estimatedMinutes: 30,
                surgeMultiplier:  1m,
                surgeReason:      null,
                expiresAt:        DateTime.UtcNow.AddMinutes(30),
                breakdown:        Array.Empty<PriceComponent>());
            return Task.FromResult(result);
        }

        public int EstimateDeliveryMinutes(decimal distanceKm) => 30;
    }
}
