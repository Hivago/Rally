using Microsoft.EntityFrameworkCore;
using RallyAPI.Catalog.Domain.MenuItems;
using RallyAPI.Catalog.Domain.Menus;
using RallyAPI.Catalog.Infrastructure.Persistence;
using RallyAPI.SharedKernel.Domain;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;
using RallyAPI.Users.Infrastructure.Persistence;

namespace RallyAPI.Host;

public class DataSeeder
{
    private readonly UsersDbContext _usersDbContext;
    private readonly CatalogDbContext _catalogDbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(
        UsersDbContext usersDbContext,
        CatalogDbContext catalogDbContext,
        IPasswordHasher passwordHasher,
        ILogger<DataSeeder> logger)
    {
        _usersDbContext = usersDbContext;
        _catalogDbContext = catalogDbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await SeedUsersAsync();
            await SeedCatalogAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task SeedUsersAsync()
    {
        if (await _usersDbContext.Customers.AnyAsync())
        {
            _logger.LogInformation("Users database already seeded.");
            return;
        }

        _logger.LogInformation("Seeding Users...");

        // 1. Admin
        var adminEmail = Email.Create("admin@rally.com").Value;
        var adminPassword = _passwordHasher.Hash("Admin@123");
        var adminResult = Admin.Create(adminEmail, adminPassword, "System Admin", AdminRole.SuperAdmin);
        if (adminResult.IsFailure) throw new Exception($"Failed to create admin: {adminResult.Error}");
        _usersDbContext.Admins.Add(adminResult.Value);

        // 2. Customer
        var customerPhone = PhoneNumber.Create("+919876543210").Value;
        var customerResult = Customer.Create(customerPhone);
        if (customerResult.IsFailure) throw new Exception($"Failed to create customer: {customerResult.Error}");
        
        var customer = customerResult.Value;
        customer.UpdateProfile("John Doe", Email.Create("john@example.com").Value);
        // Add address roughly near Koramangala
        customer.AddAddress(Address.Create(
            "123, 4th Block, Koramangala, Bengaluru - 560034", 
            "Near Wipro Park", 
            12.9340m, 
            77.6200m,
            "Home").Value);
            
        _usersDbContext.Customers.Add(customer);

        // 3. Restaurant (Spicy Bites in Koramangala)
        // Coords: 12.9352, 77.6245
        PhoneNumber restPhone = PhoneNumber.Create("+919876599999").Value;
        Email restEmail = Email.Create("spicybites@rally.com").Value;
        string restPassword = _passwordHasher.Hash("Rest@123");
        
        var restResult = Restaurant.Create(
            name: "Spicy Bites",
            phone: restPhone,
            email: restEmail,
            passwordHash: restPassword,
            addressLine: "88, Industrial Layout, Koramangala 5th Block",
            latitude: 12.9352m,
            longitude: 77.6245m);

        if (restResult.IsFailure) throw new Exception($"Failed to create restaurant: {restResult.Error}");

        var restaurant = restResult.Value;
        restaurant.SetBusinessHours(new TimeOnly(9, 0), new TimeOnly(23, 0));
        restaurant.SetPrepTime(20);
        restaurant.StartAcceptingOrders();
        
        _usersDbContext.Restaurants.Add(restaurant);

        // 4. Rider (Ramesh)
        // Coords: 12.9360, 77.6250 (Nearby)
        var riderPhone = PhoneNumber.Create("+919876500001").Value;
        var riderResult = Rider.Create(riderPhone, "Ramesh Kumar", VehicleType.Bike);
        if (riderResult.IsFailure) throw new Exception($"Failed to create rider: {riderResult.Error}");

        var rider = riderResult.Value;
        rider.UpdateProfile("Ramesh Kumar", Email.Create("ramesh@rally.com").Value, "KA-01-AB-1234");
        rider.UpdateKycStatus(KycStatus.Verified);
        
        // Need to be active before going online
        if (!rider.IsActive) rider.Activate();
        
        var onlineResult = rider.GoOnline();
        if (onlineResult.IsFailure) throw new Exception($"Failed to put rider online: {onlineResult.Error}");

        var locResult = rider.UpdateLocation(12.9360m, 77.6250m);
        if (locResult.IsFailure) throw new Exception($"Failed to update rider location: {locResult.Error}");

        _usersDbContext.Riders.Add(rider);

        await _usersDbContext.SaveChangesAsync();
        _logger.LogInformation("Users seeded successfully.");
    }

    private async Task SeedCatalogAsync()
    {
        if (await _catalogDbContext.Menus.AnyAsync())
        {
            _logger.LogInformation("Catalog database already seeded.");
            return;
        }

        // We need the restaurant ID we just created
        // Since we are in a separate context/method, we need to query for it or ensure consistent ID generation.
        // For simplicity, let's look it up by name since we know we just seeded it.
        // NOTE: In a real microservice scenario, Catalog wouldn't query Users DB directly. 
        // But here it's a modular monolith and we are seeding in the Host which has access to both.
        // We actually need to query UsersDbContext to find the restaurant ID.
        
        var restaurant = await _usersDbContext.Restaurants
            .FirstOrDefaultAsync(r => r.Name == "Spicy Bites");

        if (restaurant == null)
        {
            _logger.LogWarning("Restaurant 'Spicy Bites' not found. Skipping catalog seeding.");
            return;
        }

        _logger.LogInformation("Seeding Catalog...");

        // Create Menu
        var menu = Menu.Create(restaurant.Id, "Main Menu", "Delicious North Indian Food", 1);
        _catalogDbContext.Menus.Add(menu);

        // Create Menu Items
        var items = new List<MenuItem>
        {
            MenuItem.Create(menu.Id, restaurant.Id, "Butter Chicken", "Rich and creamy", 280m, null, 1, false, 25),
            MenuItem.Create(menu.Id, restaurant.Id, "Paneer Tikka", "Grilled cottage cheese", 220m, null, 2, true, 20),
            MenuItem.Create(menu.Id, restaurant.Id, "Garlic Naan", "Buttery garlic bread", 60m, null, 3, true, 5)
        };

        _catalogDbContext.MenuItems.AddRange(items);

        await _catalogDbContext.SaveChangesAsync();
        _logger.LogInformation("Catalog seeded successfully.");
    }
}
