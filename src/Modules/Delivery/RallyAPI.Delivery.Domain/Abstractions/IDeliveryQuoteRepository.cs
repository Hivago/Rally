using RallyAPI.Delivery.Domain.Entities;

namespace RallyAPI.Delivery.Domain.Abstractions;

public interface IDeliveryQuoteRepository
{
    Task<DeliveryQuote?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(DeliveryQuote quote, CancellationToken ct = default);
    Task UpdateAsync(DeliveryQuote quote, CancellationToken ct = default);
}