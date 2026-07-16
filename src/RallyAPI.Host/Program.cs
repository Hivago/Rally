using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RallyAPI.Catalog.Endpoints;
using RallyAPI.Delivery.Endpoints;
using RallyAPI.Host.BackgroundServices;
using RallyAPI.Host.DevEndpoints;
using RallyAPI.Host.Hubs;
using RallyAPI.Host.Services;
using RallyAPI.Infrastructure;
using RallyAPI.Integrations.ProRouting;
using RallyAPI.Marketing.Endpoints;
using RallyAPI.Orders.Endpoints;
using RallyAPI.Pricing.Infrastructure;
using RallyAPI.SharedKernel.Abstractions.Notifications;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.SharedKernel.Infrastructure;
using RallyAPI.Users.Endpoints;
using RedisRateLimiting;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
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

// Sentry error tracking. DSN is read from config "Sentry:Dsn" / env SENTRY_DSN.
// Only initialize when a DSN is actually configured: UseSentry() throws
// ArgumentNullException on a null/unset DSN (only an empty STRING no-ops). On
// Railway appsettings.json isn't in the image and SENTRY_DSN is unset -> null ->
// it crashed the whole app on startup. Guarding makes it safe with no DSN.
var sentryDsn = builder.Configuration["Sentry:Dsn"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry();
}

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    // Always write to console, even when appsettings.json (which carries the
    // Serilog config) is absent from the deployed image — otherwise post-startup
    // logs and exceptions vanish on Railway, making boot failures invisible.
    .WriteTo.Console()
    // Route Serilog Error+ events (incl. those logged by ExceptionHandlingMiddleware,
    // which catches exceptions so they never reach Sentry's ASP.NET middleware) into
    // the Sentry SDK already initialized by UseSentry(). InitializeSdk=false avoids a
    // second init; when no DSN is configured the SDK is disabled and this is a no-op.
    .WriteTo.Sentry(o =>
    {
        o.InitializeSdk = false;
        o.MinimumEventLevel = LogEventLevel.Error;
        o.MinimumBreadcrumbLevel = LogEventLevel.Information;
    }));

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

// SignalR with a Redis backplane so real-time messages fan out across every
// instance. Without this, a client connected to instance A never receives an
// event published on instance B — the hard ceiling on horizontal scaling.
builder.Services.AddSignalR()
    .AddStackExchangeRedis(
        builder.Configuration.GetConnectionString("Redis")!,
        options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal("rally-signalr");
        });
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
builder.Services.AddProRoutingIntegration(builder.Configuration, builder.Environment);

// Add Delivery Module
builder.Services.AddDeliveryModule(builder.Configuration);

// Add Marketing Module (waitlist + restaurant leads)
builder.Services.AddMarketingModule(builder.Configuration);


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

// Real-time rider location push to the customer tracking the order
builder.Services.AddScoped<ICustomerNotificationService, SignalRCustomerNotificationService>();

// Safety net: re-dispatch delivery requests wedged in a pre-assignment state when the
// inline dispatch (on the ready-for-pickup / outbox path) was interrupted and never retried.
builder.Services.AddHostedService<DeliveryDispatchRecoveryService>();


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
// Relax rate limits outside Production so the Staging test environment isn't throttled
// like prod (it runs as Production via the Dockerfile otherwise). Set
// ASPNETCORE_ENVIRONMENT=Staging on the staging service to get the lenient limits while
// keeping Swagger/dev endpoints off and the real SMS provider on.
var relaxedLimits = isDev || builder.Environment.IsStaging();

// Rate limiting is Redis-backed so limits hold ACROSS instances. With the old
// in-memory limiter, N instances meant N× the effective limit (each kept its own
// counter), defeating the protection. RedisRateLimiting reuses the registered
// IConnectionMultiplexer singleton (resolved per-request via RequestServices).
// Note: the Redis sliding-window limiter is a true sliding window (sorted-set
// based), so it has no SegmentsPerWindow knob — the prior segment counts are dropped
// but the PermitLimit/Window (the actual protection) are unchanged.
IConnectionMultiplexer ResolveRedis(HttpContext context) =>
    context.RequestServices.GetRequiredService<IConnectionMultiplexer>();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("otp", context =>
        RedisRateLimitPartition.GetSlidingWindowRateLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new RedisSlidingWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => ResolveRedis(context),
                PermitLimit = relaxedLimits ? 100 : 20,
                Window = relaxedLimits ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(10)
            }));

    options.AddPolicy("login", context =>
        RedisRateLimitPartition.GetSlidingWindowRateLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new RedisSlidingWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => ResolveRedis(context),
                PermitLimit = relaxedLimits ? 100 : 60,
                Window = relaxedLimits ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("refresh", context =>
        RedisRateLimitPartition.GetSlidingWindowRateLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new RedisSlidingWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => ResolveRedis(context),
                PermitLimit = relaxedLimits ? 100 : 60,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Public marketing lead capture: 10 requests/minute per IP in prod.
    // Used by /api/waitlist and /api/restaurant-leads (anonymous landing-page endpoints).
    options.AddPolicy("lead-capture", context =>
        RedisRateLimitPartition.GetSlidingWindowRateLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new RedisSlidingWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => ResolveRedis(context),
                PermitLimit = relaxedLimits ? 100 : 30,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Admin CSV export: 5 requests/minute per admin (by JWT sub claim).
    // Falls back to remote IP if unauthenticated, but the endpoint also requires auth.
    options.AddPolicy("admin-export", context =>
        RedisRateLimitPartition.GetFixedWindowRateLimiter(
            context.User.FindFirst("sub")?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new RedisFixedWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => ResolveRedis(context),
                PermitLimit = relaxedLimits ? 100 : 30,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.RejectionStatusCode = 429;
});

// Forwarded headers — Railway terminates TLS at its edge proxy and forwards the
// client IP in X-Forwarded-For. Without this, Connection.RemoteIpAddress is the
// proxy's IP for EVERY request, so the per-IP rate limiters above would share one
// bucket across all users (e.g. 3 OTP sends per 10 min for the whole platform).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Railway's proxy IPs are dynamic and unknown ahead of time; clear the
    // defaults (loopback-only) so the forwarded headers are honored.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// CORS — allowed browser origins are environment-specific.
//
// The committed default below is the PRODUCTION allowlist (safe by default: no
// localhost, no preview deploys). Staging widens it via the Cors:AllowedOrigins
// config — on Railway set the env var Cors__AllowedOrigins to a comma-separated
// list (localhost + *.vercel.app previews + the prod domains) so the frontend
// devs can keep pointing their localhost at the staging API. Local dev always
// gets the localhost origins appended automatically, so no config is needed there.
//
// Note: CORS origins are scheme+host+port with NO trailing slash or path — a value
// like "https://api.hivago.in" (the API itself) is never used by a browser and is
// intentionally omitted. Trailing slashes are trimmed defensively.
string[] productionOrigins =
[
    "https://hivago.in",
    "https://www.hivago.in",
    "https://admin.hivago.in",
    "https://restaurant.hivago.in",
];

string[] localhostOrigins =
[
    "http://localhost:3000",     // React dev server
    "http://localhost:5173",     // Vite dev server
    "http://localhost:4173",     // Vite preview
    "http://localhost:8081",     // Expo/React Native web
];

var configuredOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var allowedOrigins = (configuredOrigins.Length > 0 ? configuredOrigins : productionOrigins)
    .Concat(builder.Environment.IsDevelopment() ? localhostOrigins : [])
    .Select(o => o.TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

Log.Information("CORS allowed origins ({Count}): {Origins}", allowedOrigins.Length, string.Join(", ", allowedOrigins));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});




var app = builder.Build();

// Must run FIRST so everything downstream (rate limiter partitions, request
// logging, auth) sees the real client IP and scheme instead of the proxy's.
app.UseForwardedHeaders();

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
app.MapMarketingEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapPurgeOrdersByRestaurant();
    app.MapSeedRestaurantOwner();
    app.MapProRoutingDiagnosticEndpoints();
}
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapGet("/", () => "Rally API is running!");
app.MapGet("/version", (IHostEnvironment env) => Results.Ok(new
{
    version = BuildInfo.Version,
    commit = BuildInfo.Commit,
    branch = BuildInfo.Branch,
    builtAt = BuildInfo.BuildTimestampUtc,
    environment = env.EnvironmentName
}));
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

        var marketingDb = scope.ServiceProvider.GetRequiredService<RallyAPI.Marketing.Infrastructure.Persistence.MarketingDbContext>();
        logger.LogInformation("Migrating Marketing database...");
        marketingDb.Database.Migrate();

        var auditDb = scope.ServiceProvider.GetRequiredService<RallyAPI.Infrastructure.Persistence.AuditDbContext>();
        logger.LogInformation("Migrating Audit database...");
        auditDb.Database.Migrate();

        logger.LogInformation("All migrations completed successfully.");
    }
    catch (Exception ex)
    {
        // Write straight to stderr too: on Railway the Serilog config may be absent
        // and a buffered/async sink can lose this before the crash kills the process.
        Console.Error.WriteLine($"[STARTUP MIGRATION ERROR] {ex}");
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

app.Run();

}
catch (Exception ex)
{
    Console.Error.WriteLine($"[STARTUP FATAL] {ex}");
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
