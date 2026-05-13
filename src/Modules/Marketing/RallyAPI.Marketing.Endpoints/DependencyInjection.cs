using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Marketing.Application;
using RallyAPI.Marketing.Infrastructure;

namespace RallyAPI.Marketing.Endpoints;

public static class DependencyInjection
{
    public static IServiceCollection AddMarketingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMarketingApplication();
        services.AddMarketingInfrastructure(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapMarketingEndpoints(this IEndpointRouteBuilder app)
    {
        var endpointTypes = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var endpointType in endpointTypes)
        {
            var endpoint = (IEndpoint)Activator.CreateInstance(endpointType)!;
            endpoint.MapEndpoint(app);
        }

        return app;
    }
}
