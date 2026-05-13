using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.RestaurantLeads.Commands.SubmitLead;

public sealed record SubmitRestaurantLeadCommand(
    string RestaurantName,
    string OwnerName,
    string Phone,
    string City,
    int DailyOrders,
    string? Source,
    string? IpAddress) : IRequest<Result<SubmitRestaurantLeadResponse>>;

public sealed record SubmitRestaurantLeadResponse(Guid Id);
