using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Commands.AddOutlet;

public sealed record AddOutletCommand : IRequest<Result<Guid>>
{
    public Guid OwnerId { get; init; }
    public string Name { get; init; } = default!;
    public string Phone { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string AddressLine { get; init; } = default!;
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public string? FssaiNumber { get; init; }
}
