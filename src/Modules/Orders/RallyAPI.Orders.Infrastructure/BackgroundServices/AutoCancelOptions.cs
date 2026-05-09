// File: src/Modules/Orders/RallyAPI.Orders.Infrastructure/BackgroundServices/AutoCancelOptions.cs
// Purpose: Configuration for two-stage order auto-cancel
// Bound from appsettings.json section "AutoCancel"

namespace RallyAPI.Orders.Infrastructure.BackgroundServices;

public sealed class AutoCancelOptions
{
    public const string SectionName = "AutoCancel";

    /// <summary>
    /// How often (in seconds) the service checks for stale orders. Default: 30
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Stage 1: Minutes after order creation before escalating to admin. Default: 5
    /// </summary>
    public int EscalationMinutes { get; set; } = 5;

    /// <summary>
    /// Stage 2: Minutes after order creation before hard auto-cancel. Default: 10
    /// Must be greater than EscalationMinutes.
    /// </summary>
    public int HardCancelMinutes { get; set; } = 10;

    /// <summary>
    /// Minutes after Pending order creation before auto-cancelling unpaid orders. Default: 15
    /// </summary>
    public int PaymentTimeoutMinutes { get; set; } = 15;
}