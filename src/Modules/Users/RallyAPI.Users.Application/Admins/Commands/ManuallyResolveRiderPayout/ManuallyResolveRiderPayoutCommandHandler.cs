using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Commands.ManuallyResolveRiderPayout;

public sealed class ManuallyResolveRiderPayoutCommandHandler
    : IRequestHandler<ManuallyResolveRiderPayoutCommand, Result>
{
    private readonly IRiderPayoutLedgerRepository _ledgerRepository;
    private readonly IRiderPayoutExportBatchRepository _batchRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ManuallyResolveRiderPayoutCommandHandler> _logger;

    public ManuallyResolveRiderPayoutCommandHandler(
        IRiderPayoutLedgerRepository ledgerRepository,
        IRiderPayoutExportBatchRepository batchRepository,
        IUnitOfWork unitOfWork,
        ILogger<ManuallyResolveRiderPayoutCommandHandler> logger)
    {
        _ledgerRepository = ledgerRepository;
        _batchRepository = batchRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ManuallyResolveRiderPayoutCommand request, CancellationToken ct)
    {
        var payout = await _ledgerRepository.GetByIdAsync(request.PayoutId, ct);
        if (payout is null)
            return Result.Failure(Error.NotFound("Payout", request.PayoutId));

        if (payout.Status != RiderPayoutStatus.Processing)
            return Result.Failure(Error.Conflict(
                $"Cannot manually resolve a payout in {payout.Status} status — only Processing payouts (exported, awaiting reconciliation) are eligible."));

        if (request.Outcome == ManualPayoutResolutionOutcome.Paid)
        {
            var utr = request.TransactionReference!.Trim();

            if (await _ledgerRepository.ExistsWithTransactionReferenceAsync(utr, ct))
                return Result.Failure(Error.Conflict(
                    $"UTR {utr} is already recorded against another payout — refusing to record it twice."));

            payout.MarkPaid(utr);
        }
        else
        {
            payout.MarkFailed(request.Reason);
        }

        _ledgerRepository.Update(payout);

        _logger.LogWarning(
            "MANUAL OVERRIDE: rider payout {PayoutId} (rider {RiderId}) manually marked {Outcome} by admin {AdminId}. Reason: {Reason}",
            payout.Id, payout.RiderId, request.Outcome, request.ResolvedByAdminId, request.Reason);

        if (payout.ExportBatchId is { } batchId)
        {
            var batch = await _batchRepository.GetByIdAsync(batchId, ct);
            if (batch is not null && batch.Status == PayoutExportBatchStatus.Generated)
            {
                var siblings = await _ledgerRepository.GetByExportBatchIdAsync(batchId, ct);
                var stillProcessing = siblings.Any(p => p.Id != payout.Id && p.Status == RiderPayoutStatus.Processing);
                if (!stillProcessing)
                {
                    var marker = $"MANUAL-OVERRIDE:{request.ResolvedByAdminId}:{DateTime.UtcNow:O}";
                    var markerHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(marker)));
                    batch.MarkReconciled(request.ResolvedByAdminId, $"MANUAL-OVERRIDE-{markerHash}");
                    _batchRepository.Update(batch);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
