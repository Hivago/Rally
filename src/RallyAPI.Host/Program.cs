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
using RallyAPI.Users.Endpoints;
using System.Text;
using RallyAPI.Host;

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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
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
builder.Services.AddSwaggerGen();

// Add DataSeeder
builder.Services.AddScoped<DataSeeder>();

var app = builder.Build();

// Seed Database
try
{
    using (var scope = app.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();
    }
}
catch (Exception ex)
{
    Console.WriteLine("CRITICAL ERROR IN SEEDING:");
    Console.WriteLine(ex.ToString());
    throw; // Rethrow to stop startup
}

// Add Global Exception Handler (early in pipeline!)
app.UseGlobalExceptionHandler();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapUsersEndpoints();
app.MapCatalogEndpoints();
app.MapOrdersEndpoints();
app.MapDeliveryModuleEndpoints();
app.MapGet("/", () => "Rally API is running!");

app.Run();

