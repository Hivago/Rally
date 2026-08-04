using FluentAssertions;
using NSubstitute;
using RallyAPI.Orders.Application.Queries.GetStaleRestaurantPayouts;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Repositories;
using Xunit;

namespace RallyAPI.Orders.Application.Tests;

public class GetStaleRestaurantPayoutsQueryHandlerTests
{
    private readonly IPayoutRepository _payoutRepository = Substitute.For<IPayoutRepository>();

    private static readonly DateOnly PeriodStart = new(2026, 7, 13);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 19);

    [Fact]
    public async Task Handle_ReturnsStalePayouts_WithComputedDaysStale()
    {
        var ownerId = Guid.NewGuid();
        var ledger = PayoutLedger.Create(ownerId, Guid.NewGuid(), Guid.NewGuid(), "ORD-1", 550m, 50m);
        var payout = Payout.CreateFromLedger(ownerId, PeriodStart, PeriodEnd, new[] { ledger }, "111", "ICIC0001111");
        var batchId = Guid.NewGuid();
        payout.MarkProcessing(batchId);

        var handler = new GetStaleRestaurantPayoutsQueryHandler(_payoutRepository);
        _payoutRepository.GetStaleProcessingAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new[] { payout });

        var result = await handler.Handle(new GetStaleRestaurantPayoutsQuery(OlderThanDays: 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].PayoutId.Should().Be(payout.Id);
        result.Value[0].ExportBatchId.Should().Be(batchId);
        result.Value[0].DaysStale.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Handle_PassesCorrectCutoffToRepository()
    {
        var handler = new GetStaleRestaurantPayoutsQueryHandler(_payoutRepository);
        _payoutRepository.GetStaleProcessingAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Payout>());

        await handler.Handle(new GetStaleRestaurantPayoutsQuery(OlderThanDays: 5), CancellationToken.None);

        await _payoutRepository.Received(1).GetStaleProcessingAsync(
            Arg.Is<DateTime>(d => d <= DateTime.UtcNow.AddDays(-5).AddMinutes(1) && d >= DateTime.UtcNow.AddDays(-5).AddMinutes(-1)),
            Arg.Any<CancellationToken>());
    }
}
