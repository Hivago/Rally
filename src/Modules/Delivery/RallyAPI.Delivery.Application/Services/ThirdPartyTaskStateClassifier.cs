namespace RallyAPI.Delivery.Application.Services;

/// <summary>
/// What the provider's task state means for dispatch decisions.
/// </summary>
public enum ThirdPartyTaskProgress
{
    /// <summary>Provider is still looking for an agent — nobody is on this order yet.</summary>
    Searching,

    /// <summary>An agent has been assigned and is en route / working the order.</summary>
    AssignedOrBeyond,

    /// <summary>The task is dead at the provider (cancelled or failed).</summary>
    CancelledOrFailed,

    /// <summary>A state we don't recognise. Treat as "don't know" — never as "no rider".</summary>
    Unknown
}

/// <summary>
/// Maps ProRouting task states to a dispatch decision.
///
/// This exists because "our status says Searching3PL" is NOT evidence that no rider was assigned —
/// it only means no webhook reached us. Cancelling a live booking on that assumption cancels a real
/// rider mid-delivery and books (and pays for) a second one. Ask the provider, then decide.
/// </summary>
public static class ThirdPartyTaskStateClassifier
{
    public static ThirdPartyTaskProgress Classify(string? providerState)
    {
        var s = providerState?.Trim().ToLowerInvariant().Replace('_', '-') ?? string.Empty;

        if (s.Length == 0)
            return ThirdPartyTaskProgress.Unknown;

        // Still hunting for an agent.
        if (s.Contains("searching") || s is "pending" or "unfulfilled" or "created" or "new")
            return ThirdPartyTaskProgress.Searching;

        // Dead at the provider — safe to re-book.
        if (s.Contains("cancel") || s.Contains("fail") || s.Contains("reject") || s.Contains("expire"))
            return ThirdPartyTaskProgress.CancelledOrFailed;

        // Someone is on it. Includes RTO states: an RTO means a rider took the order and is
        // returning it — still not something to cancel and re-book behind.
        if (s.Contains("assigned")
            || s.Contains("pickup") || s.Contains("picked")
            || s.Contains("delivery") || s.Contains("delivered")
            || s.Contains("rto")
            || s.Contains("arrived")
            || s.Contains("transit"))
            return ThirdPartyTaskProgress.AssignedOrBeyond;

        return ThirdPartyTaskProgress.Unknown;
    }
}
