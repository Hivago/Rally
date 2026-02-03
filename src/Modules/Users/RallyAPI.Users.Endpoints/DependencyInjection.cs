using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Configuration;
using RallyAPI.Users.Application;
using RallyAPI.Users.Infrastructure;

namespace RallyAPI.Users.Endpoints;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddUsersApplication();
        services.AddUsersInfrastructure(configuration);
        services.AddUsersEndpoints();
        return services;
    }

    public static IServiceCollection AddUsersEndpoints(this IServiceCollection services)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
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