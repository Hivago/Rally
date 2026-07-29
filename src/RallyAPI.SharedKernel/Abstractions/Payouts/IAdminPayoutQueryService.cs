namespace RallyAPI.SharedKernel.Abstractions.Payouts;

/// <summary>
/// Cross-module service powering the admin Payouts page (restaurant tab).
/// Implemented in Orders.Infrastructure, consumed by Users.Application.
/// </summary>
public interface IAdminPayoutQueryService
{
    Task<RestaurantPayoutSummary> GetRestaurantSummaryAsync(
        DateTime nextAutoRunAtUtc,
        CancellationToken cancellationToken = default);

    Task<RestaurantPayoutsPagedResult> GetRestaurantPayoutsAsync(
        RestaurantPayoutsFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Order-level breakdown for a single restaurant payout, for any owner
    /// (unlike the restaurant-facing detail query, this is not owner-scoped).
    /// Returns null if the payout does not exist.
    /// </summary>
    Task<AdminPayoutDetail?> GetRestaurantPayoutDetailAsync(
        Guid payoutId,
        CancellationToken cancellationToken = default);
}

public sealed record RestaurantPayoutSummary(
    int PendingCount,
    decimal TotalPendingAmount,
    decimal FailedAmount,
    int OnHoldCount,
    decimal OnHoldAmount,
    decimal PlatformProfit,
    DateTime NextAutoRunAtUtc,
    LastAutoRunInfo? LastAutoRun);

public sealed record LastAutoRunInfo(
    DateTime AtUtc,
    int RestaurantCount,
    decimal TotalAmount,
    decimal TotalPaid);

public sealed record RestaurantPayoutsFilter(
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? OwnerId,
    string? Status, // Pending|Processing|OnHold|Paid|Failed (null = any)
    int Page,
    int PageSize);

public sealed record RestaurantPayoutsPagedResult(
    IReadOnlyList<RestaurantPayoutRow> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record RestaurantPayoutRow(
    Guid PayoutId,
    Guid OwnerId,
    string DisplayName, // "Pizza Paradise" if 1 outlet, "Owner Name (3 outlets)" otherwise
    int OrderCount,
    decimal Gmv,
    decimal NetPayable,
    string Status,
    string? StatusNote,
    DateOnly CycleStart,
    DateOnly CycleEnd,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    DateTime? ExportedAtUtc,
    string? TransactionReference);

public sealed record AdminPayoutDetail(
    Guid PayoutId,
    Guid OwnerId,
    string DisplayName,
    DateOnly CycleStart,
    DateOnly CycleEnd,
    int OrderCount,
    decimal GrossOrderAmount,
    decimal TotalGstCollected,
    decimal TotalCommission,
    decimal TotalCommissionGst,
    decimal TotalTds,
    decimal NetPayoutAmount,
    string Status,
    string? TransactionReference,
    DateTime? PaidAtUtc,
    string? Notes,
    DateTime CreatedAtUtc,
    IReadOnlyList<AdminPayoutLedgerLine> LedgerEntries);

public sealed record AdminPayoutLedgerLine(
    Guid Id,
    Guid OutletId,
    Guid OrderId,
    string OrderNumber,
    decimal OrderAmount,
    decimal GstAmount,
    decimal CommissionPercentage,
    decimal? CommissionFlatFee,
    decimal CommissionAmount,
    decimal CommissionGst,
    decimal TdsAmount,
    decimal NetAmount,
    string Status,
    DateTime CreatedAtUtc);
