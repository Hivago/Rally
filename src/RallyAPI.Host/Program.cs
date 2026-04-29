using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RallyAPI.Catalog.Endpoints;
using RallyAPI.Delivery.Endpoints;
using RallyAPI.Host.DevEndpoints;
using RallyAPI.Host.Hubs;
using RallyAPI.Host.Services;
using RallyAPI.Infrastructure;
using RallyAPI.Integrations.ProRouting;
using RallyAPI.Orders.Endpoints;
using RallyAPI.Pricing.Infrastructure;
using RallyAPI.SharedKernel.Abstractions.Notifications;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.SharedKernel.Infrastructure;
using RallyAPI.Users.Endpoints;
using Serilog;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;

// Bootstrap logger — captures startup errors before the host is built
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId());

// Serialize enums as strings in all HTTP responses (minimal API + TypedResults)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddHttpContextAccessor();

// SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<ConnectionTracker>();

builder.Services.AddScoped<DomainEventInterceptor>();
builder.Services.AddScoped<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>(sp => 
    sp.GetRequiredService<DomainEventInterceptor>());

// Add Users Module
builder.Services.AddUsersModule(builder.Configuration);

// Add Catalog Module
builder.Services.AddCatalogModule(builder.Configuration);

builder.Services.AddOrdersModule(builder.Configuration);

// Add ProRouting Integration
builder.Services.AddProRoutingIntegration(builder.Configuration);

// Add Delivery Module
builder.Services.AddDeliveryModule(builder.Configuration);


// Add Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var rsa = RSA.Create();
var publicKeyPem = jwtSettings["PublicKeyPem"];
if (!string.IsNullOrWhiteSpace(publicKeyPem))
{
    // Railway: key injected as env var JwtSettings__PublicKeyPem
    rsa.ImportFromPem(publicKeyPem.Replace("\\n", "\n"));
}
else
{
    // Local dev: read from file
    var publicKeyPath = Path.Combine(AppContext.BaseDirectory, jwtSettings["PublicKeyPath"]!);
    rsa.ImportFromPem(File.ReadAllText(publicKeyPath));
}



builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Prevent .NET from remapping "sub" → ClaimTypes.NameIdentifier etc.
        // This keeps JWT claim names as-is so FindFirst("sub") works everywhere.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new RsaSecurityKey(rsa),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };

        // SignalR WebSocket upgrade: bearer token comes via query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    context.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

   // Health Checks
   builder.Services.AddHealthChecks()
       .AddNpgSql(
           builder.Configuration.GetConnectionString("Database")!,
           name: "postgres",
           tags: new[] { "db", "ready" })
       .AddRedis(
           builder.Configuration.GetConnectionString("Redis")!,
           name: "redis",
           tags: new[] { "cache", "ready" });

// Add Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", policy =>
        policy.RequireClaim("user_type", "customer"));
    options.AddPolicy("Rider", policy =>
        policy.RequireClaim("user_type", "rider"));
    options.AddPolicy("Restaurant", policy =>
        policy.RequireClaim("user_type", "restaurant"));
    options.AddPolicy("Owner", policy =>
        policy.RequireClaim("user_type", "owner"));
    options.AddPolicy("Admin", policy =>
        policy.RequireClaim("user_type", "admin"));
    options.AddPolicy("AdminOrRestaurant", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim("user_type", "admin") ||
            ctx.User.HasClaim("user_type", "restaurant")));
    options.AddPolicy("AdminOrRider", policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("user_type", "admin") ||
        ctx.User.HasClaim("user_type", "rider")));
    options.AddPolicy("RestaurantOrAdmin", policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("user_type", "restaurant") ||
        ctx.User.HasClaim("user_type", "admin")));

    options.AddPolicy("RiderOrAdmin", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim("user_type", "rider") ||
            ctx.User.HasClaim("user_type", "admin")));
});

// Add these lines
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPricingInfrastructure(builder.Configuration);

// Register SignalR notification handlers from this assembly (avoids circular dep on IHubContext)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Override StubRiderNotificationService with real SignalR implementation
builder.Services.AddScoped<IRiderNotificationService, SignalRRiderNotificationService>();


// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Use full type names for uniqueness, but normalize nested-type separators
    // so generated $ref values remain resolver-friendly in Swagger UI.
    c.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace("+", "."));

    // 2. Your existing Security Definition
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    // 3. Your existing Security Requirement
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


// Add Rate Limiting
var isDev = builder.Environment.IsDevelopment();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("otp", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = isDev ? 100 : 3,
                Window = isDev ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(10),
                SegmentsPerWindow = 2
            }));

    options.AddPolicy("login", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = isDev ? 100 : 5,
                Window = isDev ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(15),
                SegmentsPerWindow = 3
            }));

    options.AddPolicy("refresh", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = isDev ? 100 : 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2
            }));

    // Admin CSV export: 5 requests/minute per admin (by JWT sub claim).
    // Falls back to remote IP if unauthenticated, but the endpoint also requires auth.
    options.AddPolicy("admin-export", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isDev ? 100 : 5,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.RejectionStatusCode = 429;
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",     // React dev server
                "http://localhost:5173",     // Vite dev server
                "http://localhost:8081",     // Expo/React Native web
                "https://hivago.vercel.app",   // Production 
                "https://hivago-restaurant.vercel.app",
                "http://localhost:4173")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});




var app = builder.Build();

// Add Global Exception Handler (early in pipeline!)
app.UseGlobalExceptionHandler();

// Serilog request logging — replaces default Microsoft request logging
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();


// Map endpoints
app.MapUsersEndpoints();
app.MapCatalogEndpoints();
app.MapOrdersEndpoints();
app.MapCartEndpoints();
app.MapPaymentEndpoints();
app.MapPayoutEndpoints();
app.MapDeliveryModuleEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapPurgeOrdersByRestaurant();
    app.MapSeedRestaurantOwner();
}
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapGet("/", () => "Rally API is running!");
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthCheckResponse
});

// Auto-run migrations on startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var usersDb = scope.ServiceProvider.GetRequiredService<RallyAPI.Users.Infrastructure.Persistence.UsersDbContext>();
        logger.LogInformation("Migrating Users database...");
        usersDb.Database.Migrate();

        var catalogDb = scope.ServiceProvider.GetRequiredService<RallyAPI.Catalog.Infrastructure.Persistence.CatalogDbContext>();
        logger.LogInformation("Migrating Catalog database...");
        catalogDb.Database.Migrate();

        var ordersDb = scope.ServiceProvider.GetRequiredService<RallyAPI.Orders.Infrastructure.OrdersDbContext>();
        logger.LogInformation("Migrating Orders database...");
        ordersDb.Database.Migrate();

        var deliveryDb = scope.ServiceProvider.GetRequiredService<RallyAPI.Delivery.Infrastructure.Persistence.DeliveryDbContext>();
        logger.LogInformation("Migrating Delivery database...");
        deliveryDb.Database.Migrate();

        var pricingDb = scope.ServiceProvider.GetRequiredService<RallyAPI.Pricing.Infrastructure.Persistence.PricingDbContext>();
        logger.LogInformation("Migrating Pricing database...");
        pricingDb.Database.Migrate();

        logger.LogInformation("All migrations completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


   static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var result = JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        duration = report.TotalDuration.TotalMilliseconds + "ms",
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            duration = e.Value.Duration.TotalMilliseconds + "ms",
            error = e.Value.Exception?.Message
        })
    }, new JsonSerializerOptions { WriteIndented = true });

    return context.Response.WriteAsync(result);



}
