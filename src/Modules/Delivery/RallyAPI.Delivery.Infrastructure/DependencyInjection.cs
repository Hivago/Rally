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
                configuration.GetConnectionString("Database"),
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", DeliveryDbContext.Schema));

            options.AddInterceptors(sp.GetRequiredService<ISaveChangesInterceptor>());
        });


        // Repositories
        services.AddScoped<IDeliveryQuoteRepository, DeliveryQuoteRepository>();
        services.AddScoped<IDeliveryRequestRepository, DeliveryRequestRepository>();
        services.AddScoped<IIgmTicketRepository, IgmTicketRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // IRiderNotificationService is registered by the host (SignalRRiderNotificationService).
        // Do not register the stub here — it would silently mask the real implementation
        // if the host-level override were ever removed.

        return services;
    }
}

