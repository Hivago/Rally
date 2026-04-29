namespace RallyAPI.Users.Application.Abstractions;

/// <summary>
/// Generates short, sequential, human-readable restaurant codes (e.g. "RST001").
/// Backed by a Postgres sequence so concurrent admin creates never collide.
/// </summary>
public interface IRestaurantCodeGenerator
{
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}
