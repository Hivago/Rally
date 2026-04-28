using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Cart.Abstractions;
using RallyAPI.SharedKernel.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.Orders.Infrastructure.BackgroundServices;
using RallyAPI.Orders.Infrastructure.Persistence.Repositories;
using RallyAPI.Orders.Infrastructure.Repositories;
using RallyAPI.Orders.Infrastructure.Services;
using RallyAPI.Orders.Infrastructure.Services.PayU;
using RallyAPI.SharedKernel.Abstractions.Orders;
using StackExchange.Redis;

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

        // PayU
        services.Configure<PayUOptions>(configuration.GetSection(PayUOptions.SectionName));
        services.AddHttpClient<IPayUService, PayUService>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        // Auto-cancel background service (two-stage: escalate → cancel)
        services.Configure<AutoCancelOptions>(
            configuration.GetSection(AutoCancelOptions.SectionName));
        services.AddHostedService<OrderAutoCancelService>();

        // Repositories
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<IPayoutLedgerRepository, PayoutLedgerRepository>();

        // Cart Cache (Redis write-through)
        services.AddScoped<ICartCacheService, RedisCartCacheService>();

        // Cart cleanup background service
        services.AddHostedService<CartCleanupService>();

        // Weekly payout batch creation (Mondays 6 AM IST)
        services.AddHostedService<WeeklyPayoutBatchService>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Cross-module services (consumed by admin queries via SharedKernel abstractions)
        services.AddScoped<IOrderStatsService, OrderStatsService>();
        services.AddScoped<IEscalatedOrderQueryService, EscalatedOrderQueryService>();
        services.AddScoped<ILiveOrderFeedService, LiveOrderFeedService>();
        services.AddScoped<IAdminAlertsService, AdminAlertsService>();

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