using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RallyAPI.Infrastructure.Persistence;
using RallyAPI.SharedKernel.Infrastructure;
using RallyAPI.Infrastructure.GoogleMaps;
using RallyAPI.Infrastructure.Storage;
using RallyAPI.SharedKernel.Abstractions.Distance;
using RallyAPI.SharedKernel.Abstractions.Geocoding;

namespace RallyAPI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Google Maps
        services.Configure<GoogleMapsOptions>(
            configuration.GetSection(GoogleMapsOptions.SectionName));

        var timeout = TimeSpan.FromSeconds(
            configuration.GetValue<int>("GoogleMaps:TimeoutSeconds", 10));

        services.AddHttpClient<IDistanceCalculator, GoogleMapsDistanceCalculator>(client =>
        {
            client.Timeout = timeout;
        });

        services.AddHttpClient<IGeocodingService, GoogleGeocodingService>(client =>
        {
            client.Timeout = timeout;
        });

        services.AddStorageServices(configuration);

        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));

        services.AddSingleton<RedisIdempotencyService>();

        return services;
    }
}