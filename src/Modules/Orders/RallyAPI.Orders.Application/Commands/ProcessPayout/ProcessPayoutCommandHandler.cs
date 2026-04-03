using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.ProcessPayout;

public sealed class ProcessPayoutCommandHandler : IRequestHandler<ProcessPayoutCommand, Result>
{
    private readonly IPayoutRepository _payoutRepository;
    private readonly IPayoutLedgerRepository _ledgerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessPayoutCommandHandler> _logger;

    public ProcessPayoutCommandHandler(
        IPayoutRepository payoutRepository,
        IPayoutLedgerRepository ledgerRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProcessPayoutCommandHandler> logger)
    {
        _payoutRepository = payoutRepository;
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ProcessPayoutCommand request, CancellationToken cancellationToken)
    {
        var payout = await _payoutRepository.GetByIdAsync(request.PayoutId, cancellationToken);

        if (payout is null)
            return Result.Failure(Error.NotFound("Payout not found."));

        // Transition: Pending → Processing → Paid
        payout.MarkProcessing();
        payout.MarkPaid(request.TransactionReference);

        if (!string.IsNullOrWhiteSpace(request.Notes))
            payout.AddNotes(request.Notes);

        // Mark all associated ledger entries as paid out
        var ledgerEntries = await _ledgerRepository.GetByPayoutIdAsync(payout.Id, cancellationToken);
        foreach (var entry in ledgerEntries)
        {
            entry.MarkAsPaidOut();
        }

        _payoutRepository.Update(payout);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payout {PayoutId} processed for owner {OwnerId}: net={NetAmount}, ref={TransactionReference}",
            payout.Id, payout.OwnerId, payout.NetPayoutAmount, request.TransactionReference);

        return Result.Success();
    }
}
