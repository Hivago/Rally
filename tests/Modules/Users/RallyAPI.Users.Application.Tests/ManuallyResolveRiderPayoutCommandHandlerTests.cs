using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Admins.Commands.ManuallyResolveRiderPayout;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using Xunit;

namespace RallyAPI.Users.Application.Tests;

public class ManuallyResolveRiderPayoutCommandHandlerTests
{
    private readonly IRiderPayoutLedgerRepository _ledgerRepository = Substitute.For<IRiderPayoutLedgerRepository>();
    private readonly IRiderPayoutExportBatchRepository _batchRepository = Substitute.For<IRiderPayoutExportBatchRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ManuallyResolveRiderPayoutCommandHandler _handler;

    private static readonly DateTime CycleStart = new(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CycleEnd = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AdminId = Guid.NewGuid();

    public ManuallyResolveRiderPayoutCommandHandlerTests()
    {
        _handler = new ManuallyResolveRiderPayoutCommandHandler(
            _ledgerRepository, _batchRepository, _unitOfWork,
            Substitute.For<ILogger<ManuallyResolveRiderPayoutCommandHandler>>());
    }

    private static RiderPayoutLedger ProcessingLedger(Guid batchId)
    {
        var ledger = RiderPayoutLedger.Create(Guid.NewGuid(), CycleStart, CycleEnd, deliveryCount: 5, baseFare: 500m, surgeFare: 0m, tips: 0m);
        ledger.MarkProcessing(batchId);
        return ledger;
    }

    [Fact]
    public async Task Handle_PayoutNotFound_ReturnsFailure()
    {
        _ledgerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((RiderPayoutLedger?)null);

        var result = await _handler.Handle(
            new ManuallyResolveRiderPayoutCommand(Guid.NewGuid(), ManualPayoutResolutionOutcome.Paid, "IN42619755781929", "Verified in ICICI portal", AdminId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MarkPaid_UpdatesStatus_AndReconcilesBatch_WhenNoSiblingsLeftProcessing()
    {
        var batch = RiderPayoutExportBatch.Create(
            DateOnly.FromDateTime(CycleStart), DateOnly.FromDateTime(CycleEnd), 1, 500m, AdminId, "deadbeef");
        var payout = ProcessingLedger(batch.Id);

        _ledgerRepository.GetByIdAsync(payout.Id, Arg.Any<CancellationToken>()).Returns(payout);
        _ledgerRepository.ExistsWithTransactionReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _ledgerRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { payout });

        var result = await _handler.Handle(
            new ManuallyResolveRiderPayoutCommand(payout.Id, ManualPayoutResolutionOutcome.Paid, "IN42619755781929", "Confirmed via ICICI portal statement", AdminId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payout.Status.Should().Be(RiderPayoutStatus.Paid);
        batch.Status.Should().Be(PayoutExportBatchStatus.Reconciled);
        batch.ReconciliationFileHash.Should().StartWith("MANUAL-OVERRIDE-");
    }

    [Fact]
    public async Task Handle_MarkFailed_UpdatesStatus()
    {
        var batch = RiderPayoutExportBatch.Create(
            DateOnly.FromDateTime(CycleStart), DateOnly.FromDateTime(CycleEnd), 1, 500m, AdminId, "deadbeef");
        var payout = ProcessingLedger(batch.Id);

        _ledgerRepository.GetByIdAsync(payout.Id, Arg.Any<CancellationToken>()).Returns(payout);
        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _ledgerRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { payout });

        var result = await _handler.Handle(
            new ManuallyResolveRiderPayoutCommand(payout.Id, ManualPayoutResolutionOutcome.Failed, null, "Bank confirmed account closed", AdminId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payout.Status.Should().Be(RiderPayoutStatus.Failed);
    }
}
