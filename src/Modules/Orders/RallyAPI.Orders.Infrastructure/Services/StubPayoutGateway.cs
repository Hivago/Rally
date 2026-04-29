using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.Abstractions.Payouts;

namespace RallyAPI.Orders.Infrastructure.Services;

/// <summary>
/// Development-only payout gateway. Logs the transfer and returns a synthetic
/// transaction reference. Replace with a real Razorpay/Cashfree adapter before
/// production launch.
/// </summary>
public sealed class StubPayoutGateway : IPayoutGateway
{
    private readonly ILogger<StubPayoutGateway> _logger;

    public StubPayoutGateway(ILogger<StubPayoutGateway> logger)
    {
        _logger = logger;
    }

    public Task<PayoutResult> TriggerAsync(
        Guid recipientId,
        decimal amount,
        string recipientType,
        CancellationToken cancellationToken = default)
    {
        var txnRef = $"STUB-{recipientType.ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{recipientId.ToString("N")[..8]}";

        _logger.LogInformation(
            "[StubPayoutGateway] Simulated payout: recipientType={Type} recipientId={Id} amount={Amount} txnRef={TxnRef}",
            recipientType, recipientId, amount, txnRef);

        return Task.FromResult(PayoutResult.Success(txnRef));
    }
}
