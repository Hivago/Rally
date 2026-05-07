using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Infrastructure.Persistence;
using RallyAPI.Catalog.Infrastructure.Persistence.Repositories;
using RallyAPI.Catalog.Infrastructure.Services;

namespace RallyAPI.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        //services.AddDbContext<CatalogDbContext>(options =>
        //    options.UseNpgsql(
        //        configuration.GetConnectionString("Database"),
        //        npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "catalog")));

        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Database"),
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "catalog"));

            options.AddInterceptors(sp.GetRequiredService<ISaveChangesInterceptor>());
        });

        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IMenuItemRepository, MenuItemRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Bulk-import + OCR services
        services.Configure<GoogleVisionOptions>(configuration.GetSection(GoogleVisionOptions.SectionName));
        services.AddSingleton<IMenuExcelParser, ClosedXmlMenuExcelParser>();
        services.AddSingleton<IMenuOcrService, GoogleVisionMenuOcrService>();

        return services;
    }
}