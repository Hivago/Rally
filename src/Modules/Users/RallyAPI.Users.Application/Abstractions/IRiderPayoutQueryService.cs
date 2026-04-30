namespace RallyAPI.Users.Application.Abstractions;

public interface IRiderPayoutQueryService
{
    Task<RiderPayoutSummary> GetSummaryAsync(
        DateTime nextAutoRunAtUtc,
        CancellationToken ct = default);

    Task<RiderPayoutsPagedResult> GetPayoutsAsync(
        RiderPayoutsFilter filter,
        CancellationToken ct = default);
}

public sealed record RiderPayoutSummary(
    int PendingCount,
    decimal TotalPendingAmount,
    decimal FailedAmount,
    int OnHoldCount,
    decimal OnHoldAmount,
    decimal TotalEarned,
    DateTime NextAutoRunAtUtc,
    RiderLastAutoRunInfo? LastAutoRun);

public sealed record RiderLastAutoRunInfo(
    DateTime AtUtc,
    int RiderCount,
    decimal TotalAmount,
    decimal TotalPaid);

public sealed record RiderPayoutsFilter(
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? RiderId,
    string? Status,
    int Page,
    int PageSize);

public sealed record RiderPayoutsPagedResult(
    IReadOnlyList<RiderPayoutRow> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record RiderPayoutRow(
    Guid PayoutId,
    Guid RiderId,
    string RiderName,
    string RiderPhone,
    int DeliveryCount,
    decimal BaseFare,
    decimal SurgeFare,
    decimal Tips,
    decimal NetPayable,
    string Status,
    string? StatusNote,
    DateTime CycleStart,
    DateTime CycleEnd);
