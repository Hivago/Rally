using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Admins.Commands.GenerateRiderPayoutExport;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using Xunit;

namespace RallyAPI.Users.Application.Tests;

public class GenerateRiderPayoutExportCommandHandlerTests
{
    private readonly IRiderPayoutLedgerRepository _ledgerRepository = Substitute.For<IRiderPayoutLedgerRepository>();
    private readonly IRiderPayoutExportBatchRepository _batchRepository = Substitute.For<IRiderPayoutExportBatchRepository>();
    private readonly IRiderRepository _riderRepository = Substitute.For<IRiderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GenerateRiderPayoutExportCommandHandler _handler;

    private static readonly DateTime CycleStart = new(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CycleEnd = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AdminId = Guid.NewGuid();

    public GenerateRiderPayoutExportCommandHandlerTests()
    {
        _handler = new GenerateRiderPayoutExportCommandHandler(
            _ledgerRepository, _batchRepository, _riderRepository, _unitOfWork,
            Substitute.For<ILogger<GenerateRiderPayoutExportCommandHandler>>());
    }

    private static RiderPayoutLedger PendingLedger(Guid riderId, decimal baseFare) =>
        RiderPayoutLedger.Create(riderId, CycleStart, CycleEnd, deliveryCount: 5, baseFare, surgeFare: 0m, tips: 0m);

    [Fact]
    public async Task Handle_WithNoPendingPayouts_ReturnsFailure()
    {
        _ledgerRepository.GetPendingByCycleAsync(CycleStart, CycleEnd, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RiderPayoutLedger>());

        var result = await _handler.Handle(
            new GenerateRiderPayoutExportCommand(CycleStart, CycleEnd, AdminId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ExcludesRidersWithMissingBankDetails_AndIncludesTheRest()
    {
        var riderWithBank = Guid.NewGuid();
        var riderWithoutBank = Guid.NewGuid();
        var payoutWithBank = PendingLedger(riderWithBank, 500m);
        var payoutWithoutBank = PendingLedger(riderWithoutBank, 300m);

        _ledgerRepository.GetPendingByCycleAsync(CycleStart, CycleEnd, Arg.Any<CancellationToken>())
            .Returns(new[] { payoutWithBank, payoutWithoutBank });

        _riderRepository.GetBankDetailsByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, RiderBankDetails>
            {
                [riderWithBank] = new RiderBankDetails(riderWithBank, "9876543210", "ICIC0005678", "Sunil Dinesh"),
                [riderWithoutBank] = new RiderBankDetails(riderWithoutBank, null, null, null)
            });

        var result = await _handler.Handle(
            new GenerateRiderPayoutExportCommand(CycleStart, CycleEnd, AdminId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RowCount.Should().Be(1);
        result.Value.Excluded.Should().ContainSingle(e => e.RiderId == riderWithoutBank);
        result.Value.ControlSumTotal.Should().Be(payoutWithBank.NetPayable);

        payoutWithBank.Status.Should().Be(RiderPayoutStatus.Processing);
        payoutWithoutBank.Status.Should().Be(RiderPayoutStatus.Pending);
    }

    [Fact]
    public async Task Handle_WhenAllPayoutsMissingBankDetails_ReturnsFailure()
    {
        var rider = Guid.NewGuid();
        var payout = PendingLedger(rider, 500m);

        _ledgerRepository.GetPendingByCycleAsync(CycleStart, CycleEnd, Arg.Any<CancellationToken>())
            .Returns(new[] { payout });
        _riderRepository.GetBankDetailsByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, RiderBankDetails>());

        var result = await _handler.Handle(
            new GenerateRiderPayoutExportCommand(CycleStart, CycleEnd, AdminId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        payout.Status.Should().Be(RiderPayoutStatus.Pending);
    }

    [Fact]
    public async Task Handle_ExcludesZeroNetPayout_InsteadOfCrashingOrExportingIt()
    {
        var zeroRider = Guid.NewGuid();
        var goodRider = Guid.NewGuid();
        var zeroNetPayout = PendingLedger(zeroRider, baseFare: 0m);
        var goodPayout = PendingLedger(goodRider, baseFare: 500m);

        zeroNetPayout.NetPayable.Should().Be(0m); // sanity-check the fixture itself

        _ledgerRepository.GetPendingByCycleAsync(CycleStart, CycleEnd, Arg.Any<CancellationToken>())
            .Returns(new[] { zeroNetPayout, goodPayout });
        _riderRepository.GetBankDetailsByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, RiderBankDetails>
            {
                [zeroRider] = new RiderBankDetails(zeroRider, "111", "ICIC0000001", "Zero Rider"),
                [goodRider] = new RiderBankDetails(goodRider, "222", "ICIC0000002", "Good Rider")
            });

        var result = await _handler.Handle(
            new GenerateRiderPayoutExportCommand(CycleStart, CycleEnd, AdminId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RowCount.Should().Be(1);
        result.Value.Excluded.Should().ContainSingle(e => e.PayoutId == zeroNetPayout.Id);
        zeroNetPayout.Status.Should().Be(RiderPayoutStatus.Pending);
    }
}
