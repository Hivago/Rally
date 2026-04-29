using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Infrastructure.Persistence;

namespace RallyAPI.Users.Infrastructure.Services;

/// <summary>
/// Pulls the next value from the users.restaurant_rst_code_seq sequence and
/// formats it as RST### (zero-padded to 3 digits, expands automatically beyond 999).
/// </summary>
public sealed class RestaurantCodeGenerator : IRestaurantCodeGenerator
{
    private readonly UsersDbContext _context;

    public RestaurantCodeGenerator(UsersDbContext context)
    {
        _context = context;
    }

    public async Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        var next = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('users.restaurant_rst_code_seq') AS \"Value\"")
            .FirstAsync(cancellationToken);

        return $"RST{next:D3}";
    }
}
