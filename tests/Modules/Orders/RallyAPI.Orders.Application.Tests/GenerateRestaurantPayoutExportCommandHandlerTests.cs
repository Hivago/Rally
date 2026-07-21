using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Commands.GenerateRestaurantPayoutExport;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using Xunit;

namespace RallyAPI.Orders.Application.Tests;

public class GenerateRestaurantPayoutExportCommandHandlerTests
{
    private readonly IPayoutRepository _payoutRepository = Substitute.For<IPayoutRepository>();
    private readonly IRestaurantPayoutExportBatchRepository _batchRepository = Substitute.For<IRestaurantPayoutExportBatchRepository>();
    private readonly IRestaurantQueryService _restaurantQueryService = Substitute.For<IRestaurantQueryService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GenerateRestaurantPayoutExportCommandHandler _handler;

    private static readonly DateOnly PeriodStart = new(2026, 7, 13);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 19);
    private static readonly Guid AdminId = Guid.NewGuid();

    public GenerateRestaurantPayoutExportCommandHandlerTests()
    {
        _handler = new GenerateRestaurantPayoutExportCommandHandler(
            _payoutRepository, _batchRepository, _restaurantQueryService, _unitOfWork,
            Substitute.For<ILogger<GenerateRestaurantPayoutExportCommandHandler>>());
    }

    private static Payout PendingPayout(Guid ownerId, decimal orderAmount, decimal commissionFlatFee)
    {
        var ledger = PayoutLedger.Create(ownerId, Guid.NewGuid(), Guid.NewGuid(), orderAmount, commissionFlatFee);
        return Payout.CreateFromLedger(ownerId, PeriodStart, PeriodEnd, new[] { ledger }, null, null);
    }

    [Fact]
    public async Task Handle_WithNoPendingPayouts_ReturnsFailure()
    {
        _payoutRepository.GetPendingByPeriodAsync(PeriodStart, PeriodEnd, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Payout>());

        var result = await _handler.Handle(
            new GenerateRestaurantPayoutExportCommand(PeriodStart, PeriodEnd, AdminId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ExcludesOwnersWithMissingBankDetails_AndIncludesTheRest()
    {
        var ownerWithBank = Guid.NewGuid();
        var ownerWithoutBank = Guid.NewGuid();
        var payoutWithBank = PendingPayout(ownerWithBank, 500m, 30m);
        var payoutWithoutBank = PendingPayout(ownerWithoutBank, 1000m, 55m);

        _payoutRepository.GetPendingByPeriodAsync(PeriodStart, PeriodEnd, Arg.Any<CancellationToken>())
            .Returns(new[] { payoutWithBank, payoutWithoutBank });

        _restaurantQueryService.GetOwnerBankDetailsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, OwnerBankDetails>
            {
                [ownerWithBank] = new OwnerBankDetails(ownerWithBank, "1234567890", "ICIC0001234", "Sharma Foods"),
                [ownerWithoutBank] = new OwnerBankDetails(ownerWithoutBank, null, null, null)
            });

        var result = await _handler.Handle(
            new GenerateRestaurantPayoutExportCommand(PeriodStart, PeriodEnd, AdminId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RowCount.Should().Be(1);
        result.Value.Excluded.Should().ContainSingle(e => e.OwnerId == ownerWithoutBank);
        result.Value.ControlSumTotal.Should().Be(payoutWithBank.NetPayoutAmount);

        // The included payout must flip to Processing so it can never be re-exported —
        // the excluded one stays Pending so it's picked up once bank details are fixed.
        payoutWithBank.Status.Should().Be(PayoutStatus.Processing);
        payoutWithoutBank.Status.Should().Be(PayoutStatus.Pending);
    }

    [Fact]
    public async Task Handle_ExcludesNegativeNetPayout_InsteadOfCrashingOrExportingIt()
    {
        // Misconfigured commission (flat fee exceeds the order amount) drives NetPayoutAmount
        // negative — must be excluded, never wired to the bank file or allowed to throw.
        var badOwner = Guid.NewGuid();
        var goodOwner = Guid.NewGuid();
        var negativeNetPayout = PendingPayout(badOwner, orderAmount: 150m, commissionFlatFee: 200m);
        var goodPayout = PendingPayout(goodOwner, orderAmount: 500m, commissionFlatFee: 30m);

        negativeNetPayout.NetPayoutAmount.Should().BeNegative(); // sanity-check the fixture itself

        _payoutRepository.GetPendingByPeriodAsync(PeriodStart, PeriodEnd, Arg.Any<CancellationToken>())
            .Returns(new[] { negativeNetPayout, goodPayout });
        _restaurantQueryService.GetOwnerBankDetailsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, OwnerBankDetails>
            {
                [badOwner] = new OwnerBankDetails(badOwner, "111", "ICIC0000001", "Bad Owner"),
                [goodOwner] = new OwnerBankDetails(goodOwner, "222", "ICIC0000002", "Good Owner")
            });

        var result = await _handler.Handle(
            new GenerateRestaurantPayoutExportCommand(PeriodStart, PeriodEnd, AdminId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RowCount.Should().Be(1);
        result.Value.Excluded.Should().ContainSingle(e => e.PayoutId == negativeNetPayout.Id);
        result.Value.ControlSumTotal.Should().BePositive();
        negativeNetPayout.Status.Should().Be(PayoutStatus.Pending); // left untouched for admin review
    }

    [Fact]
    public async Task Handle_WhenAllPayoutsMissingBankDetails_ReturnsFailure()
    {
        var owner = Guid.NewGuid();
        var payout = PendingPayout(owner, 500m, 30m);

        _payoutRepository.GetPendingByPeriodAsync(PeriodStart, PeriodEnd, Arg.Any<CancellationToken>())
            .Returns(new[] { payout });
        _restaurantQueryService.GetOwnerBankDetailsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, OwnerBankDetails>());

        var result = await _handler.Handle(
            new GenerateRestaurantPayoutExportCommand(PeriodStart, PeriodEnd, AdminId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        payout.Status.Should().Be(PayoutStatus.Pending);
    }

    [Fact]
    public async Task Handle_ControlSumTotal_EqualsSumOfIncludedNetAmounts()
    {
        var owner1 = Guid.NewGuid();
        var owner2 = Guid.NewGuid();
        var payout1 = PendingPayout(owner1, 333.33m, 33.33m);
        var payout2 = PendingPayout(owner2, 1000m, 55.55m);

        _payoutRepository.GetPendingByPeriodAsync(PeriodStart, PeriodEnd, Arg.Any<CancellationToken>())
            .Returns(new[] { payout1, payout2 });
        _restaurantQueryService.GetOwnerBankDetailsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, OwnerBankDetails>
            {
                [owner1] = new OwnerBankDetails(owner1, "111", "ICIC0000001", "Owner One"),
                [owner2] = new OwnerBankDetails(owner2, "222", "ICIC0000002", "Owner Two")
            });

        var result = await _handler.Handle(
            new GenerateRestaurantPayoutExportCommand(PeriodStart, PeriodEnd, AdminId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ControlSumTotal.Should().Be(payout1.NetPayoutAmount + payout2.NetPayoutAmount);
        result.Value.FileContent.Should().NotBeEmpty();
    }
}
