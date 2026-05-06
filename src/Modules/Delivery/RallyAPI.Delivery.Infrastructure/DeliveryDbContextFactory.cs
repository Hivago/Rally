using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using RallyAPI.Delivery.Infrastructure.Persistence;

namespace RallyAPI.Delivery.Infrastructure;

/// <summary>
/// Design-time factory for EF Core CLI tools (migrations, database update).
/// Bypasses the full app startup so Redis, JWT, etc. are not required.
/// </summary>
internal sealed class DeliveryDbContextFactory : IDesignTimeDbContextFactory<DeliveryDbContext>
{
    public DeliveryDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Database");

        var optionsBuilder = new DbContextOptionsBuilder<DeliveryDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", DeliveryDbContext.Schema);
        });

        return new DeliveryDbContext(optionsBuilder.Options);
    }
}
