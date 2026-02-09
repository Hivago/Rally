using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Delivery.Application;
using RallyAPI.Delivery.Infrastructure;

namespace RallyAPI.Delivery.Endpoints;

public static class DependencyInjection
{
    public static IServiceCollection AddDeliveryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDeliveryInfrastructure(configuration);
        services.AddDeliveryApplication(configuration);

        return services;
    }

    public static IEndpointRouteBuilder MapDeliveryModuleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapDeliveryEndpoints();
        app.MapRiderDeliveryEndpoints();
        app.MapTrackingEndpoints();
        app.MapWebhookEndpoints();

        return app;
    }
}