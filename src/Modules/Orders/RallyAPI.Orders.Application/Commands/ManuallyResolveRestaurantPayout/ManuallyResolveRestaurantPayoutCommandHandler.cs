using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.ManuallyResolveRestaurantPayout;

public sealed class ManuallyResolveRestaurantPayoutCommandHandler
    : IRequestHandler<ManuallyResolveRestaurantPayoutCommand, Result>
{
    private readonly IPayoutRepository _payoutRepository;
    private readonly IRestaurantPayoutExportBatchRepository _batchRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ManuallyResolveRestaurantPayoutCommandHandler> _logger;

    public ManuallyResolveRestaurantPayoutCommandHandler(
        IPayoutRepository payoutRepository,
        IRestaurantPayoutExportBatchRepository batchRepository,
        IUnitOfWork unitOfWork,
        ILogger<ManuallyResolveRestaurantPayoutCommandHandler> logger)
    {
        _payoutRepository = payoutRepository;
        _batchRepository = batchRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ManuallyResolveRestaurantPayoutCommand request, CancellationToken ct)
    {
        var payout = await _payoutRepository.GetByIdAsync(request.PayoutId, ct);
        if (payout is null)
            return Result.Failure(Error.NotFound("Payout", request.PayoutId));

        if (payout.Status != PayoutStatus.Processing)
            return Result.Failure(Error.Conflict(
                $"Cannot manually resolve a payout in {payout.Status} status — only Processing payouts (exported, awaiting reconciliation) are eligible."));

        if (request.Outcome == ManualPayoutResolutionOutcome.Paid)
        {
            var utr = request.TransactionReference!.Trim();

            if (await _payoutRepository.ExistsWithTransactionReferenceAsync(utr, ct))
                return Result.Failure(Error.Conflict(
                    $"UTR {utr} is already recorded against another payout — refusing to record it twice."));

            payout.MarkPaid(utr);
        }
        else
        {
            payout.MarkFailed(request.Reason);
        }

        _payoutRepository.Update(payout);

        _logger.LogWarning(
            "MANUAL OVERRIDE: restaurant payout {PayoutId} (owner {OwnerId}) manually marked {Outcome} by admin {AdminId}. Reason: {Reason}",
            payout.Id, payout.OwnerId, request.Outcome, request.ResolvedByAdminId, request.Reason);

        if (payout.ExportBatchId is { } batchId)
        {
            var batch = await _batchRepository.GetByIdAsync(batchId, ct);
            if (batch is not null && batch.Status == PayoutExportBatchStatus.Generated)
            {
                var siblings = await _payoutRepository.GetByExportBatchIdAsync(batchId, ct);
                var stillProcessing = siblings.Any(p => p.Id != payout.Id && p.Status == PayoutStatus.Processing);
                if (!stillProcessing)
                {
                    // No reconciliation file backs this closure — stamp a marker that's
                    // visibly not a SHA-256 file hash, so a later audit can tell the batch
                    // was closed by manual override rather than an automatic reconcile.
                    // ReconciliationFileHash is HasMaxLength(64) (sized for a real SHA-256
                    // hex digest) — the "MANUAL-" prefix + truncated hash must fit inside it.
                    var marker = $"MANUAL-OVERRIDE:{request.ResolvedByAdminId}:{DateTime.UtcNow:O}";
                    var markerHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(marker)));
                    batch.MarkReconciled(request.ResolvedByAdminId, $"MANUAL-{markerHash[..56]}");
                    _batchRepository.Update(batch);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
