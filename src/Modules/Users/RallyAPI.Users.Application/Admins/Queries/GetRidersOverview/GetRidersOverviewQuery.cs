using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Queries.RiderOverview;

public record RiderOverviewQuery(Guid RiderId) : IRequest<Result<RiderOverviewResponseDTO>>;