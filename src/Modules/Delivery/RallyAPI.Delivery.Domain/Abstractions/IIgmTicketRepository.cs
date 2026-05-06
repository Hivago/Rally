using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Domain.Abstractions;

public interface IIgmTicketRepository
{
    Task<IgmTicket?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IgmTicket?> GetByExternalIssueIdAsync(string externalIssueId, CancellationToken ct = default);
    Task<IReadOnlyList<IgmTicket>> GetByDeliveryRequestIdAsync(Guid deliveryRequestId, CancellationToken ct = default);
    Task<IReadOnlyList<IgmTicket>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<IgmTicket>> GetByStateAsync(IgmTicketState state, CancellationToken ct = default);
    Task AddAsync(IgmTicket ticket, CancellationToken ct = default);
    Task UpdateAsync(IgmTicket ticket, CancellationToken ct = default);
}
