using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Infrastructure.Persistence;
using RallyAPI.Delivery.Infrastructure.Repositories;

namespace RallyAPI.Delivery.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDeliveryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        //// Database
        //services.AddDbContext<DeliveryDbContext>(options =>
        //    options.UseNpgsql(
        //        configuration.GetConnectionString("DefaultConnection"),
        //        b => b.MigrationsHistoryTable("__EFMigrationsHistory", DeliveryDbContext.Schema)));


        services.AddDbContext<DeliveryDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", DeliveryDbContext.Schema));

            options.AddInterceptors(sp.GetRequiredService<ISaveChangesInterceptor>());
        });


        // Repositories
        services.AddScoped<IDeliveryQuoteRepository, DeliveryQuoteRepository>();
        services.AddScoped<IDeliveryRequestRepository, DeliveryRequestRepository>();

        // Services
        services.AddScoped<RallyAPI.SharedKernel.Abstractions.Notifications.IRiderNotificationService, Services.StubRiderNotificationService>();

        return services;
    }
}

