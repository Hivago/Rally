using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RallyAPI.Catalog.Endpoints;
using RallyAPI.Infrastructure;
using RallyAPI.Integrations.ProRouting;
using RallyAPI.Orders.Endpoints;
using RallyAPI.Pricing.Infrastructure;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Endpoints;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add this BEFORE other service registrations
builder.Services.AddHttpContextAccessor();

// Add Users Module
builder.Services.AddUsersModule(builder.Configuration);

// Add Catalog Module
builder.Services.AddCatalogModule(builder.Configuration);

// Add Order Module
builder.Services.AddOrdersModule(builder.Configuration);

// Add ProRouting Integration
builder.Services.AddProRoutingIntegration(builder.Configuration);

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
app.UseAuthorization();

// Map endpoints
app.MapUsersEndpoints();
app.MapCatalogEndpoints();
app.MapOrdersEndpoints();
app.MapGet("/", () => "Rally API is running!");

app.Run();

