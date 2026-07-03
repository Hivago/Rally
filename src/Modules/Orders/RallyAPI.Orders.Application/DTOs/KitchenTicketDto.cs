using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Application.DTOs;

/// <summary>
/// Kitchen Order Ticket (KOT) — the print-oriented, kitchen-facing view of an order.
/// Deliberately omits all pricing/money fields: the kitchen only needs to know
/// WHAT to cook, HOW MUCH, and any preparation notes.
/// </summary>
public sealed record KitchenTicketDto
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;

    // Fulfillment context — kitchens plate/pack differently for pickup vs delivery
    public FulfillmentType FulfillmentType { get; init; }
    public string FulfillmentDisplay { get; init; } = string.Empty;

    // Minimal customer identity (name only — no phone/address/pricing on a KOT)
    public string CustomerName { get; init; } = string.Empty;

    // Current lifecycle status, for reprint context (e.g. "Preparing")
    public string StatusDisplay { get; init; } = string.Empty;

    // When the customer placed/paid — printed at the top of the ticket
    public DateTime PlacedAt { get; init; }

    // The lines the kitchen actually cooks
    public IReadOnlyList<KitchenTicketItemDto> Items { get; init; } = Array.Empty<KitchenTicketItemDto>();
    public int TotalItems { get; init; }

    // Order-level preparation note (e.g. "No onions in anything", "Ring bell")
    public string? SpecialInstructions { get; init; }
}

/// <summary>
/// A single cook line on the kitchen ticket.
/// </summary>
public sealed record KitchenTicketItemDto
{
    public string ItemName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string? SpecialInstructions { get; init; }
}
