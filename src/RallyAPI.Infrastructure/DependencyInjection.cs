using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Infrastructure.GoogleMaps;
using RallyAPI.SharedKernel.Abstractions.Distance;

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

        services.AddHttpClient<IDistanceCalculator, GoogleMapsDistanceCalculator>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue<int>("GoogleMaps:TimeoutSeconds", 10));
        });

        return services;
    }
}