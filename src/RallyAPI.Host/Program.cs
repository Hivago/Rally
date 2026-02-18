using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RallyAPI.Catalog.Endpoints;
using RallyAPI.Delivery.Endpoints;
using RallyAPI.Infrastructure;
using RallyAPI.Integrations.ProRouting;
using RallyAPI.Orders.Endpoints;
using RallyAPI.Orders.Infrastructure;
using RallyAPI.Pricing.Infrastructure;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.SharedKernel.Infrastructure;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Endpoints;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<DomainEventInterceptor>();
builder.Services.AddScoped<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>(sp => 
    sp.GetRequiredService<DomainEventInterceptor>());

// Add Users Module
builder.Services.AddUsersModule(builder.Configuration);

// Add Catalog Module
builder.Services.AddCatalogModule(builder.Configuration);

// Add Order Module
builder.Services.AddOrdersModule(builder.Configuration);

// Add ProRouting Integration
builder.Services.AddProRoutingIntegration(builder.Configuration);

// Add Delivery Module
builder.Services.AddDeliveryModule(builder.Configuration);


// Add Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var publicKeyText = File.ReadAllText(jwtSettings["PublicKeyPath"]!);
var rsa = RSA.Create();
rsa.ImportFromPem(publicKeyText);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new RsaSecurityKey(rsa)
        };
    });

// Add Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", policy =>
        policy.RequireClaim("user_type", "customer"));
    options.AddPolicy("Rider", policy =>
        policy.RequireClaim("user_type", "rider"));
    options.AddPolicy("Restaurant", policy =>
        policy.RequireClaim("user_type", "restaurant"));
    options.AddPolicy("Admin", policy =>
        policy.RequireClaim("user_type", "admin"));
});

// Add these lines
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPricingInfrastructure(builder.Configuration);


// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Rate limit for OTP requests: 3 per 10 minutes per IP
    options.AddPolicy("otp", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2
            }));

    // Rate limit for login: 5 per 15 minutes per IP
    options.AddPolicy("login", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 3
            }));

    // Rate limit for token refresh: 10 per minute per IP
    options.AddPolicy("refresh", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2
            }));

    options.RejectionStatusCode = 429; // Too Many Requests
});


var app = builder.Build();

// Add Global Exception Handler (early in pipeline!)
app.UseGlobalExceptionHandler();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();


/**

Now we need to apply these policies to the endpoints. Paste your endpoint files so I know the exact methods to add `.RequireRateLimiting()` to:
```
src / Modules / Users / RallyAPI.Users.Endpoints / Customers / SendOtp.cs
src / Modules / Users / RallyAPI.Users.Endpoints / Admins / Login.cs
src / Modules / Users / RallyAPI.Users.Endpoints / Restaurants / Login.cs
src / Modules / Users / RallyAPI.Users.Endpoints / Riders / SendOtp.cs
**/


// Map endpoints
app.MapUsersEndpoints();
app.MapCatalogEndpoints();
app.MapOrdersEndpoints();
app.MapDeliveryModuleEndpoints();
app.MapGet("/", () => "Rally API is running!");

app.Run();

