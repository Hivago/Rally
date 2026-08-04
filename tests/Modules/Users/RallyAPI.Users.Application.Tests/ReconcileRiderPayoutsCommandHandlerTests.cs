using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Admins.Commands.ReconcileRiderPayouts;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using Xunit;

namespace RallyAPI.Users.Application.Tests;

public class ReconcileRiderPayoutsCommandHandlerTests
{
    private readonly IRiderPayoutLedgerRepository _ledgerRepository = Substitute.For<IRiderPayoutLedgerRepository>();
    private readonly IRiderPayoutExportBatchRepository _batchRepository = Substitute.For<IRiderPayoutExportBatchRepository>();
    private readonly IRiderRepository _riderRepository = Substitute.For<IRiderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ReconcileRiderPayoutsCommandHandler _handler;

    private static readonly DateTime CycleStart = new(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CycleEnd = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AdminId = Guid.NewGuid();

    public ReconcileRiderPayoutsCommandHandlerTests()
    {
        _handler = new ReconcileRiderPayoutsCommandHandler(
            _ledgerRepository, _batchRepository, _riderRepository, _unitOfWork,
            Substitute.For<ILogger<ReconcileRiderPayoutsCommandHandler>>());
    }

    private static RiderPayoutLedger ProcessingLedger(Guid riderId, decimal baseFare, Guid batchId)
    {
        var ledger = RiderPayoutLedger.Create(riderId, CycleStart, CycleEnd, deliveryCount: 5, baseFare, surgeFare: 0m, tips: 0m);
        ledger.MarkProcessing(batchId);
        return ledger;
    }

    private static RiderPayoutExportBatch GeneratedBatch(int rowCount, decimal controlSum)
        => RiderPayoutExportBatch.Create(
            DateOnly.FromDateTime(CycleStart), DateOnly.FromDateTime(CycleEnd), rowCount, controlSum, AdminId, "deadbeef");

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

    private static ReconcileRiderPayoutsCommand Command(Guid batchId, byte[] fileBytes)
        => new(batchId, new MemoryStream(fileBytes), fileBytes, AdminId);

    [Fact]
    public async Task Handle_BatchNotFound_ReturnsFailure()
    {
        _batchRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((RiderPayoutExportBatch?)null);

        var result = await _handler.Handle(Command(Guid.NewGuid(), BuildReport()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MatchedSuccessRow_MarksPaidAndReconcilesBatch_UsingLiveBankDetails()
    {
        var batch = GeneratedBatch(1, 500m);
        var rider = Guid.NewGuid();
        var ledger = ProcessingLedger(rider, 500m, batch.Id);

        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _ledgerRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { ledger });
        _riderRepository.GetBankDetailsByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, RiderBankDetails>
            {
                [rider] = new RiderBankDetails(rider, "9876543210", "ICIC0005555", "Rider One")
            });
        _ledgerRepository.ExistsWithTransactionReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var fileBytes = BuildReport(("Rider One", "9876543210", "ICIC0005555", ledger.NetPayable, "Success", "Paid", "", "IN42619755782929"));

        var result = await _handler.Handle(Command(batch.Id, fileBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MarkedPaid.Should().Be(1);
        result.Value.BatchFullyReconciled.Should().BeTrue();
        ledger.Status.Should().Be(RiderPayoutStatus.Paid);
        ledger.TransactionReference.Should().Be("IN42619755782929");
        batch.Status.Should().Be(PayoutExportBatchStatus.Reconciled);
    }

    [Fact]
    public async Task Handle_RiderMissingLiveBankDetails_NeverMatched_StaysProcessing()
    {
        var batch = GeneratedBatch(1, 500m);
        var rider = Guid.NewGuid();
        var ledger = ProcessingLedger(rider, 500m, batch.Id);

        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _ledgerRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { ledger });
        // Rider's bank details vanished/changed since export — no entry returned.
        _riderRepository.GetBankDetailsByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, RiderBankDetails>());

        var fileBytes = BuildReport(("Rider One", "9876543210", "ICIC0005555", ledger.NetPayable, "Success", "Paid", "", "IN42619755782929"));

        var result = await _handler.Handle(Command(batch.Id, fileBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MarkedPaid.Should().Be(0);
        result.Value.Unresolved.Should().ContainSingle();
        ledger.Status.Should().Be(RiderPayoutStatus.Processing);
    }

    [Fact]
    public async Task Handle_ReversedRow_MarksFailed()
    {
        var batch = GeneratedBatch(1, 500m);
        var rider = Guid.NewGuid();
        var ledger = ProcessingLedger(rider, 500m, batch.Id);

        _batchRepository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _ledgerRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(new[] { ledger });
        _riderRepository.GetBankDetailsByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, RiderBankDetails>
            {
                [rider] = new RiderBankDetails(rider, "9876543210", "ICIC0005555", "Rider One")
            });

        var fileBytes = BuildReport(("Rider One", "9876543210", "ICIC0005555", ledger.NetPayable, "Reversed", "Reversed", "", ""));

        var result = await _handler.Handle(Command(batch.Id, fileBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MarkedFailed.Should().Be(1);
        ledger.Status.Should().Be(RiderPayoutStatus.Failed);
    }
}
