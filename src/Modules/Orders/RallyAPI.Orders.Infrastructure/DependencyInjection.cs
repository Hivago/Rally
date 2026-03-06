using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Infrastructure.BackgroundServices;
using RallyAPI.Orders.Infrastructure.Repositories;
using RallyAPI.Orders.Infrastructure.Services;

namespace RallyAPI.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext
        //var connectionString = configuration.GetConnectionString("Database");
        //services.AddDbContext<OrdersDbContext>(options =>
        //{
        //    options.UseNpgsql(connectionString, npgsqlOptions =>
        //    {
        //        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "orders");
        //    });
        //});

        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<OrdersDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "orders");
            });

            options.AddInterceptors(sp.GetRequiredService<ISaveChangesInterceptor>());
        });

        // Auto-cancel background service (two-stage: escalate → cancel)
        services.Configure<AutoCancelOptions>(
            configuration.GetSection(AutoCancelOptions.SectionName));
        services.AddHostedService<OrderAutoCancelService>();

        // Repositories
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Services
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<IOrderValidationService, OrderValidationService>();
        services.AddScoped<IOrderPricingService, OrderPricingService>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Options
        services.Configure<PricingOptions>(
            configuration.GetSection(PricingOptions.SectionName));

        return services;
    }
}