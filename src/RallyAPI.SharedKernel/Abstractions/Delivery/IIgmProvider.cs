namespace RallyAPI.SharedKernel.Abstractions.Delivery;

/// <summary>
/// Issue/Grievance Management (IGM) operations against a 3PL provider.
/// Maps to ProRouting's /partner/order/issue, /issue_status, /issue_close endpoints.
///
/// Kept separate from <see cref="IThirdPartyDeliveryProvider"/> so the two
/// concerns can be mocked / tested independently.
/// </summary>
public interface IIgmProvider
{
    string ProviderName { get; }

    Task<RaiseIssueResult> RaiseIssueAsync(
        RaiseIssueRequest request,
        CancellationToken ct = default);

    Task<IssueStatusResult> GetIssueStatusAsync(
        string externalIssueId,
        CancellationToken ct = default);

    Task<CloseIssueResult> CloseIssueAsync(
        CloseIssueRequest request,
        CancellationToken ct = default);
}

public sealed record RaiseIssueRequest
{
    public required string ExternalTaskId { get; init; }
    public required string Category { get; init; }       // "FULFILLMENT"
    public required string SubCategory { get; init; }    // "FLM02" / "FLM03" / "FLM08"
    public required string DescriptionShort { get; init; }
    public string? DescriptionLong { get; init; }
}

public sealed record RaiseIssueResult
{
    private RaiseIssueResult() { }

    public bool IsSuccess { get; private init; }
    public string? ExternalIssueId { get; private init; }
    public string? State { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string? ProviderName { get; private init; }

    public static RaiseIssueResult Success(string externalIssueId, string state, string providerName) =>
        new() { IsSuccess = true, ExternalIssueId = externalIssueId, State = state, ProviderName = providerName };

    public static RaiseIssueResult Failure(string errorMessage, string providerName) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage, ProviderName = providerName };
}

public sealed record IssueStatusResult
{
    private IssueStatusResult() { }

    public bool IsSuccess { get; private init; }
    public string? ExternalIssueId { get; private init; }
    public string? State { get; private init; }
    public string? ResolutionShortDesc { get; private init; }
    public string? ResolutionLongDesc { get; private init; }
    public string? ActionTriggered { get; private init; }   // "REFUND" / "NO-ACTION"
    public decimal? RefundAmount { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string? ProviderName { get; private init; }

    public static IssueStatusResult Success(
        string externalIssueId,
        string state,
        string? resolutionShortDesc,
        string? resolutionLongDesc,
        string? actionTriggered,
        decimal? refundAmount,
        string providerName) =>
        new()
        {
            IsSuccess = true,
            ExternalIssueId = externalIssueId,
            State = state,
            ResolutionShortDesc = resolutionShortDesc,
            ResolutionLongDesc = resolutionLongDesc,
            ActionTriggered = actionTriggered,
            RefundAmount = refundAmount,
            ProviderName = providerName
        };

    public static IssueStatusResult Failure(string errorMessage, string providerName) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage, ProviderName = providerName };
}

public sealed record CloseIssueRequest
{
    public required string ExternalIssueId { get; init; }
    public required string Rating { get; init; }      // "THUMBS-UP" / "THUMBS-DOWN"
    public bool RefundByLsp { get; init; }
    public bool RefundToClient { get; init; }
}

public sealed record CloseIssueResult
{
    private CloseIssueResult() { }

    public bool IsSuccess { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string? ProviderName { get; private init; }

    public static CloseIssueResult Success(string providerName) =>
        new() { IsSuccess = true, ProviderName = providerName };

    public static CloseIssueResult Failure(string errorMessage, string providerName) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage, ProviderName = providerName };
}
