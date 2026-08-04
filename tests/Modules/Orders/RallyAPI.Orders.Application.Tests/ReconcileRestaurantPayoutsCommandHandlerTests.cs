using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Commands.ReconcileRestaurantPayouts;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;
using Xunit;

namespace RallyAPI.Orders.Application.Tests;

public class ReconcileRestaurantPayoutsCommandHandlerTests
{
    private readonly IPayoutRepository _payoutRepository = Substitute.For<IPayoutRepository>();
    private readonly IRestaurantPayoutExportBatchRepository _batchRepository = Substitute.For<IRestaurantPayoutExportBatchRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ReconcileRestaurantPayoutsCommandHandler _handler;

    private static readonly DateOnly PeriodStart = new(2026, 7, 13);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 19);
    private static readonly Guid AdminId = Guid.NewGuid();

    public ReconcileRestaurantPayoutsCommandHandlerTests()
    {
        _handler = new ReconcileRestaurantPayoutsCommandHandler(
            _payoutRepository, _batchRepository, _unitOfWork,
            Substitute.For<ILogger<ReconcileRestaurantPayoutsCommandHandler>>());
    }

    private static Payout ProcessingPayout(
        Guid ownerId, decimal orderAmount, decimal commissionFlatFee,
        string accountNumber, string ifsc, Guid batchId)
    {
        var ledger = PayoutLedger.Create(ownerId, Guid.NewGuid(), Guid.NewGuid(), "ORD-20260713-00001", orderAmount, commissionFlatFee);
        var payout = Payout.CreateFromLedger(ownerId, PeriodStart, PeriodEnd, new[] { ledger }, accountNumber, ifsc);
        payout.MarkProcessing(batchId);
        return payout;
    }

    private static RestaurantPayoutExportBatch GeneratedBatch(int rowCount, decimal controlSum)
        => RestaurantPayoutExportBatch.Create(PeriodStart, PeriodEnd, rowCount, controlSum, AdminId, "deadbeef");

    private static readonly string[] Headers =
    {
        "Beneficiary Name", "Beneficiary Account No", "Bene_IFSC_Code", "Amount",
        "STATUS", "Current Step", "Rejection Reason", "UTR NO"
    };

    private static byte[] BuildReport(params (string Name, string Account, string Ifsc, decimal Amount, string Status, string Step, string Reason, string Utr)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");
        for (var c = 0; c < Headers.Length; c++) sheet.Cell(1, c + 1).Value = Headers[c];

        for (var r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            sheet.Cell(r + 2, 1).Value = row.Name;
            sheet.Cell(r + 2, 2).Value = row.Account;
            sheet.Cell(r + 2, 3).Value = row.Ifsc;
            sheet.Cell(r + 2, 4).Value = row.Amount;
            sheet.Cell(r + 2, 5).Value = row.Status;
            sheet.Cell(r + 2, 6).Value = row.Step;
            sheet.Cell(r + 2, 7).Value = row.Reason;
            sheet.Cell(r + 2, 8).Value = row.Utr;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static ReconcileRestaurantPayoutsCommand Command(Guid batchId, byte[] fileBytes)
        => new(batchId, new MemoryStream(fileBytes), fileBytes, AdminId);

    [Fact]
    public async Task Handle_BatchNotFound_ReturnsFailure()
    {
        _batchRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((RestaurantPayoutExportBatch?)null);

        var result = await _handler.Handle(Command(Guid.NewGuid(), BuildReport()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MatchedSuccessRow_MarksPaidAndReconcilesBatch()
    {
        var batch = GeneratedBatch(1, 500m);
        var owner = Guid.NewGuid();
        var payout = ProcessingPayout(owner, 550m, 50m, "1234567890", "ICIC0001234", batch.Id);

        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _payoutRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { payout });
        _payoutRepository.ExistsWithTransactionReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var fileBytes = BuildReport(("Owner", "1234567890", "ICIC0001234", payout.NetPayoutAmount, "Success", "Paid", "", "IN42619755781929"));

        var result = await _handler.Handle(Command(batch.Id, fileBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MarkedPaid.Should().Be(1);
        result.Value.BatchFullyReconciled.Should().BeTrue();
        payout.Status.Should().Be(PayoutStatus.Paid);
        payout.TransactionReference.Should().Be("IN42619755781929");
        batch.Status.Should().Be(PayoutExportBatchStatus.Reconciled);
    }

    [Fact]
    public async Task Handle_ReversedRow_MarksFailed_BatchNotYetReconciledIfOthersStillProcessing()
    {
        var batch = GeneratedBatch(2, 1000m);
        var owner1 = Guid.NewGuid();
        var owner2 = Guid.NewGuid();
        var failedPayout = ProcessingPayout(owner1, 550m, 50m, "1111111111", "ICIC0001111", batch.Id);
        var stillPending = ProcessingPayout(owner2, 550m, 50m, "2222222222", "ICIC0002222", batch.Id);

        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _payoutRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { failedPayout, stillPending });

        var fileBytes = BuildReport(
            ("Owner1", "1111111111", "ICIC0001111", failedPayout.NetPayoutAmount, "Reversed", "Reversed", "", ""));

        var result = await _handler.Handle(Command(batch.Id, fileBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MarkedFailed.Should().Be(1);
        result.Value.BatchFullyReconciled.Should().BeFalse();
        failedPayout.Status.Should().Be(PayoutStatus.Failed);
        stillPending.Status.Should().Be(PayoutStatus.Processing);
        batch.Status.Should().Be(PayoutExportBatchStatus.Generated);
    }

    [Fact]
    public async Task Handle_DuplicateUtr_LeavesPayoutProcessing_AndReportsUnresolved()
    {
        var batch = GeneratedBatch(1, 500m);
        var owner = Guid.NewGuid();
        var payout = ProcessingPayout(owner, 550m, 50m, "1234567890", "ICIC0001234", batch.Id);

        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _payoutRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { payout });
        _payoutRepository.ExistsWithTransactionReferenceAsync("IN42619755781929", Arg.Any<CancellationToken>()).Returns(true);

        var fileBytes = BuildReport(("Owner", "1234567890", "ICIC0001234", payout.NetPayoutAmount, "Success", "Paid", "", "IN42619755781929"));

        var result = await _handler.Handle(Command(batch.Id, fileBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MarkedPaid.Should().Be(0);
        result.Value.Unresolved.Should().ContainSingle(u => u.Reason.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
        payout.Status.Should().Be(PayoutStatus.Processing);
    }

    [Fact]
    public async Task Handle_NoMatchingPayout_ReportsUnresolved_NeverGuesses()
    {
        var batch = GeneratedBatch(1, 500m);
        var owner = Guid.NewGuid();
        var payout = ProcessingPayout(owner, 550m, 50m, "1234567890", "ICIC0001234", batch.Id);

        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _payoutRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { payout });

        // Amount doesn't match anything in the batch.
        var fileBytes = BuildReport(("Owner", "1234567890", "ICIC0001234", 999999m, "Success", "Paid", "", "IN42619755781929"));

        var result = await _handler.Handle(Command(batch.Id, fileBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Unresolved.Should().ContainSingle();
        payout.Status.Should().Be(PayoutStatus.Processing);
        batch.Status.Should().Be(PayoutExportBatchStatus.Generated);
    }

    [Fact]
    public async Task Handle_ReUploadAfterFullReconciliation_IsIdempotent_NoOp()
    {
        var batch = GeneratedBatch(1, 500m);
        var owner = Guid.NewGuid();
        var payout = ProcessingPayout(owner, 550m, 50m, "1234567890", "ICIC0001234", batch.Id);
        payout.MarkPaid("IN42619755781929");
        batch.MarkReconciled(AdminId, "originalhash");

        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _payoutRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { payout });

        var fileBytes = BuildReport(("Owner", "1234567890", "ICIC0001234", payout.NetPayoutAmount, "Success", "Paid", "", "IN42619755781929"));

        var result = await _handler.Handle(Command(batch.Id, fileBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AlreadyResolvedSkipped.Should().Be(1);
        result.Value.MarkedPaid.Should().Be(0);
        result.Value.BatchFullyReconciled.Should().BeTrue();
        payout.Status.Should().Be(PayoutStatus.Paid); // unchanged
    }

    [Fact]
    public async Task Handle_MalformedUtr_DoesNotMarkPaid()
    {
        var batch = GeneratedBatch(1, 500m);
        var owner = Guid.NewGuid();
        var payout = ProcessingPayout(owner, 550m, 50m, "1234567890", "ICIC0001234", batch.Id);

        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _payoutRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { payout });

        var fileBytes = BuildReport(("Owner", "1234567890", "ICIC0001234", payout.NetPayoutAmount, "Success", "Paid", "", "BAD"));

        var result = await _handler.Handle(Command(batch.Id, fileBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MarkedPaid.Should().Be(0);
        result.Value.Unresolved.Should().ContainSingle();
        payout.Status.Should().Be(PayoutStatus.Processing);
    }
}
