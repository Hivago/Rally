// RallyAPI.Pricing.Infrastructure/DependencyInjection.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Pricing.Application.Abstractions;
using RallyAPI.Pricing.Application.Rules;
using RallyAPI.Pricing.Application.Services;
using RallyAPI.Pricing.Domain.Abstractions;
using RallyAPI.Pricing.Domain.Repositories;
using RallyAPI.Pricing.Infrastructure.Persistence;
using RallyAPI.Pricing.Infrastructure.Providers;
using RallyAPI.Pricing.Infrastructure.Repositories;
using RallyAPI.SharedKernel.Abstractions.Pricing;
using RallyAPI.SharedKernel.Infrastructure;
using RallyAPI.Pricing.Infrastructure.Services;

namespace RallyAPI.Pricing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPricingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")!;

        // Delivery pricing
        services.Configure<DeliveryPricingOptions>(
            configuration.GetSection(DeliveryPricingOptions.SectionName));

        //// DbContext
        //services.AddDbContext<PricingDbContext>(options =>
        //    options.UseNpgsql(connectionString));
        
        services.AddDbContext<PricingDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<DomainEventInterceptor>());
        });



        // Repository
        services.AddScoped<IPricingConfigRepository, PricingConfigRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PricingDbContext>());

        // Rules (easy to add/remove)
        services.AddScoped<IPricingRule, BaseFeeRule>();
        services.AddScoped<IPricingRule, DistanceRule>();
        services.AddScoped<IPricingRule, TimeSurgeRule>();
        services.AddScoped<IPricingRule, WeatherSurgeRule>();
        services.AddScoped<IPricingRule, DemandSurgeRule>();
        services.AddScoped<IPricingRule, SpecialDayRule>();
        services.AddScoped<IDeliveryPricingCalculator, DeliveryPricingCalculator>();


        // Engine
        services.AddScoped<IPricingEngine, PricingEngine>();

        // Providers
        services.AddHttpClient<IWeatherProvider, WeatherProvider>();
        services.AddScoped<IDemandTracker>(_ => new DemandTracker(
            _.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            connectionString));

        // Cache
        services.AddMemoryCache();

        return services;
    }
}