namespace RallyAPI.Delivery.Domain.Enums;

/// <summary>
/// Local IGM ticket lifecycle. Mirrors ProRouting issue states + adds Open
/// (raised locally but not yet pushed to ProRouting).
/// </summary>
public enum IgmTicketState
{
    /// <summary>
    /// Created locally, not yet pushed to ProRouting.
    /// </summary>
    Open = 1,

    /// <summary>
    /// Pushed to ProRouting via /issue. Mirrors their "Processing".
    /// </summary>
    Processing = 2,

    /// <summary>
    /// ProRouting returned a resolution via /issue_status.
    /// </summary>
    Resolved = 3,

    /// <summary>
    /// Closed via /issue_close.
    /// </summary>
    Closed = 4
}
