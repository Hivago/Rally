using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RallyAPI.Users.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI tools (migrations, database update).
/// Bypasses the full app startup so Redis, JWT, etc. are not required.
/// Must be public for EF tooling to discover it.
/// </summary>
public sealed class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
    public UsersDbContext CreateDbContext(string[] args)
    {
        // Look for appsettings.json in current dir first (Host project),
        // then fall back to the Host project relative to this project.
        var basePath = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"))
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "RallyAPI.Host"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Database")
            ?? "Host=localhost;Port=5432;Database=rally;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "users");
        });

        return new UsersDbContext(optionsBuilder.Options);
    }
}
