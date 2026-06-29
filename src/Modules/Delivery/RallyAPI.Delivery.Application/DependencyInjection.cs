using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Delivery.Application.Behaviors;
using RallyAPI.Delivery.Application.Services;

namespace RallyAPI.Delivery.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDeliveryApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // MediatR + validation pipeline
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // FluentValidation validators (MarkPickedUp, MarkDelivered, DeclineDeliveryOffer, ...)
        services.AddValidatorsFromAssembly(assembly);

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