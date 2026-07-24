using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Infrastructure.Persistence;
using RallyAPI.Users.Infrastructure.Persistence.Repositories;
using RallyAPI.Users.Infrastructure.Services;
using RallyAPI.SharedKernel.Abstractions.Riders;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StackExchange.Redis;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.Users.Infrastructure.BackgroundServices;
namespace RallyAPI.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDatabase(configuration);

        // Repositories
        services.AddRepositories();

        // Services
        services.AddServices(configuration);

        return services;
    }

    private static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");

        //services.AddDbContext<UsersDbContext>(options =>
        //{
        //    options.UseNpgsql(connectionString, npgsqlOptions =>
        //    {
        //        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "users");
        //    });
        //});

        services.AddDbContext<UsersDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "users");
            });

            options.AddInterceptors(sp.GetRequiredService<ISaveChangesInterceptor>());
        });

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IRiderRepository, RiderRepository>();
        services.AddScoped<IRestaurantRepository, RestaurantRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IRestaurantOwnerRepository, RestaurantOwnerRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRestaurantQueryService, RestaurantQueryService>();
        services.AddScoped<IRestaurantCodeGenerator, RestaurantCodeGenerator>();
        services.AddScoped<IRiderPayoutLedgerRepository, RiderPayoutLedgerRepository>();
        services.AddScoped<IRiderPayoutQueryService, RiderPayoutQueryService>();
        services.AddScoped<IRiderPayoutExportBatchRepository, RiderPayoutExportBatchRepository>();
        services.AddScoped<IRestaurantTimeOffRepository, RestaurantTimeOffRepository>();

        // Cross-module availability probe — consumed by Orders.PlaceOrder
        services.AddScoped<IRestaurantAvailabilityChecker, RestaurantAvailabilityChecker>();

        // Rider services for cross-module communication
        services.AddScoped<IRiderQueryService, RiderQueryService>();
        services.AddScoped<IRiderCommandService, RiderCommandService>();

        return services;
    }

    private static IServiceCollection AddServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // JWT Settings
        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));

        // Services
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        // Redis
        var redisConnection = configuration.GetConnectionString("Redis")!;
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection));


        // OTP delivery provider — chosen via "OtpProvider" config key.
        // Supported values: "AuthKey", "Msg91WhatsApp", "Console" (default for dev).
        // Falls back to whichever provider section exists if OtpProvider is absent.
        var otpProvider = (configuration["OtpProvider"] ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(otpProvider))
        {
            if (configuration.GetSection(AuthKeyOptions.SectionName).Exists())
                otpProvider = "AuthKey";
            else if (configuration.GetSection(Msg91WhatsAppOptions.SectionName).Exists())
                otpProvider = "Msg91WhatsApp";
            else
                otpProvider = "Console";
        }

        switch (otpProvider)
        {
            case "AuthKey":
                services.Configure<AuthKeyOptions>(
                    configuration.GetSection(AuthKeyOptions.SectionName));

                services.AddHttpClient<ISmsService, AuthKeyOtpService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
                break;

            case "Msg91WhatsApp":
                services.Configure<Msg91WhatsAppOptions>(
                    configuration.GetSection(Msg91WhatsAppOptions.SectionName));

                services.AddHttpClient<ISmsService, Msg91WhatsAppService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
                break;

            case "Console":
                // Dev-only stub: logs the OTP instead of sending an SMS.
                services.AddSingleton<ISmsService, ConsoleSmsService>();
                break;

            default:
                // An explicit but unrecognized OtpProvider value must NEVER silently
                // fall back to the console stub — that drops every OTP while the API
                // still reports success. Incident (2026-06-08): staging had
                // OtpProvider accidentally set to the AuthKey API key, so it routed
                // to ConsoleSmsService and no SMS ever sent. Fail fast at startup.
                throw new InvalidOperationException(
                    $"Unrecognized OtpProvider value '{otpProvider}'. " +
                    "Valid values are 'AuthKey', 'Msg91WhatsApp', or 'Console'. " +
                    "Check the OtpProvider environment variable / appsettings.");
        }

        services.AddScoped<IOtpService, OtpService>();
        services.Configure<RiderPayoutDispatchOptions>(
            configuration.GetSection(RiderPayoutDispatchOptions.SectionName));
        services.AddHostedService<RiderPayoutAggregationJob>();

        return services;
    }
}
