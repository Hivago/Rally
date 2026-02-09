using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Delivery.Application.Services;

namespace RallyAPI.Delivery.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDeliveryApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // MediatR
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // Options
        services.Configure<PrepTimeOptions>(
            configuration.GetSection(PrepTimeOptions.SectionName));

        services.Configure<DispatchOptions>(
            configuration.GetSection(DispatchOptions.SectionName));

        // Services
        services.AddScoped<PrepTimeCalculator>();
        services.AddScoped<RiderDispatchOrchestrator>();

        return services;
    }
}