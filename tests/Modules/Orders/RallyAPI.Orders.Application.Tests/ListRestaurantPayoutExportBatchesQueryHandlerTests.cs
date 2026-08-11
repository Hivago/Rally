using FluentAssertions;
using NSubstitute;
using RallyAPI.Orders.Application.Queries.ListRestaurantPayoutExportBatches;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;
using Xunit;

namespace RallyAPI.Orders.Application.Tests;

public class ListRestaurantPayoutExportBatchesQueryHandlerTests
{
    private readonly IRestaurantPayoutExportBatchRepository _batchRepository = Substitute.For<IRestaurantPayoutExportBatchRepository>();
    private readonly IPayoutRepository _payoutRepository = Substitute.For<IPayoutRepository>();
    private readonly ListRestaurantPayoutExportBatchesQueryHandler _handler;

    private static readonly DateOnly PeriodStart = new(2026, 7, 13);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 19);
    private static readonly Guid AdminId = Guid.NewGuid();

    public ListRestaurantPayoutExportBatchesQueryHandlerTests()
    {
        _handler = new ListRestaurantPayoutExportBatchesQueryHandler(_batchRepository, _payoutRepository);
    }

    [Fact]
    public async Task Handle_ReturnsBatchSummary_WithLiveStatusBreakdown()
    {
        var batch = RestaurantPayoutExportBatch.Create(PeriodStart, PeriodEnd, 2, 1000m, AdminId, "deadbeef");

        var ownerId1 = Guid.NewGuid();
        var ledger1 = PayoutLedger.Create(ownerId1, Guid.NewGuid(), Guid.NewGuid(), "ORD-1", 550m, 50m);
        var paidPayout = Payout.CreateFromLedger(ownerId1, PeriodStart, PeriodEnd, new[] { ledger1 }, "111", "ICIC0001111");
        paidPayout.MarkProcessing(batch.Id, "111", "ICIC0001111");
        paidPayout.MarkPaid("IN42619755781929");

        var ownerId2 = Guid.NewGuid();
        var ledger2 = PayoutLedger.Create(ownerId2, Guid.NewGuid(), Guid.NewGuid(), "ORD-2", 550m, 50m);
        var stuckPayout = Payout.CreateFromLedger(ownerId2, PeriodStart, PeriodEnd, new[] { ledger2 }, "222", "ICIC0002222");
        stuckPayout.MarkProcessing(batch.Id, "222", "ICIC0002222");

        _batchRepository.GetRecentAsync(null, 0, 20, Arg.Any<CancellationToken>())
            .Returns(new[] { batch });
        _payoutRepository.GetByExportBatchIdAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { paidPayout, stuckPayout });

        var result = await _handler.Handle(
            new ListRestaurantPayoutExportBatchesQuery(null, Page: 1, PageSize: 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        var summary = result.Value[0];
        summary.Id.Should().Be(batch.Id);
        summary.PaidCount.Should().Be(1);
        summary.ProcessingCount.Should().Be(1);
        summary.FailedCount.Should().Be(0);
        summary.Status.Should().Be(PayoutExportBatchStatus.Generated);
    }
}
