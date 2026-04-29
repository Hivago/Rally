namespace RallyAPI.SharedKernel.Abstractions.Payouts;

/// <summary>
/// Bank-rail abstraction for triggering an outgoing transfer to a restaurant owner or rider.
/// Implemented as a no-op stub today (returns success with a synthetic transaction ref);
/// real Razorpay/Cashfree integration plugs in here later without changing call sites.
/// </summary>
public interface IPayoutGateway
{
    Task<PayoutResult> TriggerAsync(
        Guid recipientId,
        decimal amount,
        string recipientType, // "Restaurant" or "Rider"
        CancellationToken cancellationToken = default);
}

public sealed record PayoutResult(
    bool IsSuccess,
    string? TransactionReference,
    string? FailureReason)
{
    public static PayoutResult Success(string transactionReference) =>
        new(true, transactionReference, null);

    public static PayoutResult Failure(string reason) =>
        new(false, null, reason);
}
