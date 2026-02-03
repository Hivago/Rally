using Microsoft.EntityFrameworkCore;
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
        // Database
        services.AddDbContext<DeliveryDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", DeliveryDbContext.Schema)));

        // Repositories
        services.AddScoped<IDeliveryQuoteRepository, DeliveryQuoteRepository>();
        services.AddScoped<IDeliveryRequestRepository, DeliveryRequestRepository>();

        return services;
    }
}

