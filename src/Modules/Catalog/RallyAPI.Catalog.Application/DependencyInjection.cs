using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Catalog.Application.Behaviors;

namespace RallyAPI.Catalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            // Constrained to IMenuCacheInvalidatingCommand — only menu-write commands pass through it.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(MenuCacheInvalidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
