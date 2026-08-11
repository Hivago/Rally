using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Commands.ManuallyResolveRestaurantPayout;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;
using Xunit;

namespace RallyAPI.Orders.Application.Tests;

public class ManuallyResolveRestaurantPayoutCommandHandlerTests
{
    private readonly IPayoutRepository _payoutRepository = Substitute.For<IPayoutRepository>();
    private readonly IRestaurantPayoutExportBatchRepository _batchRepository = Substitute.For<IRestaurantPayoutExportBatchRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ManuallyResolveRestaurantPayoutCommandHandler _handler;

    private static readonly DateOnly PeriodStart = new(2026, 7, 13);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 19);
    private static readonly Guid AdminId = Guid.NewGuid();

    public ManuallyResolveRestaurantPayoutCommandHandlerTests()
    {
        _handler = new ManuallyResolveRestaurantPayoutCommandHandler(
            _payoutRepository, _batchRepository, _unitOfWork,
            Substitute.For<ILogger<ManuallyResolveRestaurantPayoutCommandHandler>>());
    }

    private static Payout ProcessingPayout(Guid batchId)
    {
        var ownerId = Guid.NewGuid();
        var ledger = PayoutLedger.Create(ownerId, Guid.NewGuid(), Guid.NewGuid(), "ORD-1", 550m, 50m);
        var payout = Payout.CreateFromLedger(ownerId, PeriodStart, PeriodEnd, new[] { ledger }, "111", "ICIC0001111");
        payout.MarkProcessing(batchId, "111", "ICIC0001111");
        return payout;
    }

    [Fact]
    public async Task Handle_PayoutNotFound_ReturnsFailure()
    {
        _payoutRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Payout?)null);

        var result = await _handler.Handle(
            new ManuallyResolveRestaurantPayoutCommand(Guid.NewGuid(), ManualPayoutResolutionOutcome.Paid, "IN42619755781929", "Verified in ICICI portal", AdminId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PayoutNotProcessing_ReturnsConflict()
    {
        var batchId = Guid.NewGuid();
        var payout = ProcessingPayout(batchId);
        payout.MarkPaid("IN42619755781000"); // already resolved

        _payoutRepository.GetByIdAsync(payout.Id, Arg.Any<CancellationToken>()).Returns(payout);

        var result = await _handler.Handle(
            new ManuallyResolveRestaurantPayoutCommand(payout.Id, ManualPayoutResolutionOutcome.Paid, "IN42619755781929", "Verified in ICICI portal", AdminId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MarkPaid_UpdatesStatus_AndReconcilesBatch_WhenNoSiblingsLeftProcessing()
    {
        var batch = RestaurantPayoutExportBatch.Create(PeriodStart, PeriodEnd, 1, 500m, AdminId, "deadbeef");
        var payout = ProcessingPayout(batch.Id);

        _payoutRepository.GetByIdAsync(payout.Id, Arg.Any<CancellationToken>()).Returns(payout);
        _payoutRepository.ExistsWithTransactionReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _payoutRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { payout });

        var result = await _handler.Handle(
            new ManuallyResolveRestaurantPayoutCommand(payout.Id, ManualPayoutResolutionOutcome.Paid, "IN42619755781929", "Confirmed via ICICI portal statement", AdminId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payout.Status.Should().Be(PayoutStatus.Paid);
        payout.TransactionReference.Should().Be("IN42619755781929");
        batch.Status.Should().Be(PayoutExportBatchStatus.Reconciled);
        batch.ReconciliationFileHash.Should().StartWith("MANUAL-");
        batch.ReconciliationFileHash!.Length.Should().BeLessOrEqualTo(64);
    }

    [Fact]
    public async Task Handle_MarkPaid_DuplicateUtr_ReturnsFailure_LeavesPayoutProcessing()
    {
        var batch = RestaurantPayoutExportBatch.Create(PeriodStart, PeriodEnd, 1, 500m, AdminId, "deadbeef");
        var payout = ProcessingPayout(batch.Id);

        _payoutRepository.GetByIdAsync(payout.Id, Arg.Any<CancellationToken>()).Returns(payout);
        _payoutRepository.ExistsWithTransactionReferenceAsync("IN42619755781929", Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(
            new ManuallyResolveRestaurantPayoutCommand(payout.Id, ManualPayoutResolutionOutcome.Paid, "IN42619755781929", "Confirmed via ICICI portal statement", AdminId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        payout.Status.Should().Be(PayoutStatus.Processing);
    }

    [Fact]
    public async Task Handle_MarkFailed_UpdatesStatus_BatchStaysGenerated_IfSiblingsStillProcessing()
    {
        var batch = RestaurantPayoutExportBatch.Create(PeriodStart, PeriodEnd, 2, 1000m, AdminId, "deadbeef");
        var payout = ProcessingPayout(batch.Id);
        var sibling = ProcessingPayout(batch.Id);

        _payoutRepository.GetByIdAsync(payout.Id, Arg.Any<CancellationToken>()).Returns(payout);
        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _payoutRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { payout, sibling });

        var result = await _handler.Handle(
            new ManuallyResolveRestaurantPayoutCommand(payout.Id, ManualPayoutResolutionOutcome.Failed, null, "Bank confirmed account closed", AdminId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payout.Status.Should().Be(PayoutStatus.Failed);
        batch.Status.Should().Be(PayoutExportBatchStatus.Generated);
    }
}
