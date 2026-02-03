using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Domain.Errors;

public static class DeliveryErrors
{
    public static Error QuoteNotFound(Guid quoteId) =>
        Error.NotFound("DeliveryQuote", quoteId);

    public static Error QuoteExpired(Guid quoteId) =>
        Error.Validation($"Quote {quoteId} has expired.");

    public static Error QuoteAlreadyUsed(Guid quoteId) =>
        Error.Validation($"Quote {quoteId} has already been used.");

    public static Error DeliveryRequestNotFound(Guid requestId) =>
        Error.NotFound("DeliveryRequest", requestId);

    public static Error DeliveryRequestNotFoundByOrder(Guid orderId) =>
        Error.Validation($"No delivery request found for order {orderId}.");

    public static readonly Error NoRidersAvailable =
        Error.Validation("No riders available for delivery.");

    public static readonly Error AllRidersRejected =
        Error.Validation("All riders rejected or timed out.");

    public static readonly Error ThirdPartyBookingFailed =
        Error.Validation("Failed to book 3PL delivery.");

    public static Error InvalidStatusTransition(string current, string attempted) =>
        Error.Validation($"Cannot transition from {current} to {attempted}.");

    public static readonly Error OfferExpired =
        Error.Validation("This delivery offer has expired.");

    public static readonly Error OfferAlreadyResponded =
        Error.Validation("This offer has already been responded to.");

    public static readonly Error RiderNotAssigned =
        Error.Validation("No rider assigned to this delivery.");

    public static readonly Error CannotCancelAfterPickup =
        Error.Validation("Cannot cancel delivery after pickup.");
}