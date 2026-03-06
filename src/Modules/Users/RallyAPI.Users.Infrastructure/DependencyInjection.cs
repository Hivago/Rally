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
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRestaurantQueryService, RestaurantQueryService>();


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


        // MSG91 WhatsApp OTP delivery — or Console fallback for dev
        if (configuration.GetSection(Msg91WhatsAppOptions.SectionName).Exists())
        {
            services.Configure<Msg91WhatsAppOptions>(
                configuration.GetSection(Msg91WhatsAppOptions.SectionName));

            services.AddHttpClient<ISmsService, Msg91WhatsAppService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        }
        else
        {
            services.AddSingleton<ISmsService, ConsoleSmsService>();
        }

        services.AddScoped<IOtpService, OtpService>();

        return services;
    }
}