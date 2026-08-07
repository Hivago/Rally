using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Infrastructure.Persistence;
using RallyAPI.Marketing.Infrastructure.Persistence.Repositories;
using RallyAPI.Marketing.Infrastructure.Services;
using RallyAPI.SharedKernel.Security;

namespace RallyAPI.Marketing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMarketingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<MarketingDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Database"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "marketing"));

            options.AddInterceptors(sp.GetRequiredService<ISaveChangesInterceptor>());
        });

        services.AddScoped<ICustomerWaitlistRepository, CustomerWaitlistRepository>();
        services.AddScoped<IRestaurantLeadRepository, RestaurantLeadRepository>();
        services.AddScoped<IRestaurantOnboardingApplicationRepository, RestaurantOnboardingApplicationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
        services.AddSingleton<IFieldEncryptionService, AesGcmFieldEncryptionService>();

        services.Configure<DiscordNotificationOptions>(configuration.GetSection(DiscordNotificationOptions.SectionName));
        services.AddHttpClient<IOnboardingNotificationService, DiscordOnboardingNotificationService>();

        return services;
    }
}
