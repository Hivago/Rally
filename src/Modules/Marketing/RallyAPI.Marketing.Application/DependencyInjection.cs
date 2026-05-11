using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Marketing.Application.Behaviors;

namespace RallyAPI.Marketing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMarketingApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
