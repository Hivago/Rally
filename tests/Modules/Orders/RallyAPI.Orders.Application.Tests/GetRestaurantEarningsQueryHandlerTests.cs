using FluentAssertions;
using NSubstitute;
using RallyAPI.Orders.Application.Queries.GetRestaurantEarnings;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Repositories;
using Xunit;

namespace RallyAPI.Orders.Application.Tests;

/// <summary>
/// Pins the field-by-field mapping from PayoutLedger to PayoutLedgerDto for the earnings
/// endpoint — this handler builds the DTO independently of GetPayoutDetail/GetGstSummary/
/// GetTdsSummary, so a field added to PayoutLedger can silently stay blank here even when
/// the other three handlers are updated correctly.
/// </summary>
public class GetRestaurantEarningsQueryHandlerTests
{
    private readonly IPayoutLedgerRepository _ledgerRepository = Substitute.For<IPayoutLedgerRepository>();
    private readonly GetRestaurantEarningsQueryHandler _handler;

    private static readonly Guid OwnerId = Guid.NewGuid();

    public GetRestaurantEarningsQueryHandlerTests()
    {
        _handler = new GetRestaurantEarningsQueryHandler(_ledgerRepository);
    }

    [Fact]
    public async Task Handle_MapsOrderNumber_OntoLedgerEntryDto()
    {
        var ledger = PayoutLedger.Create(
            OwnerId, Guid.NewGuid(), Guid.NewGuid(), "ORD-20260727-00042", 160m, 50m);

        _ledgerRepository.GetByOwnerIdAndDateRangeAsync(
                OwnerId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new[] { ledger });

        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6));
        var toDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await _handler.Handle(
            new GetRestaurantEarningsQuery { OwnerId = OwnerId, FromDate = fromDate, ToDate = toDate },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LedgerEntries.Should().ContainSingle()
            .Which.OrderNumber.Should().Be("ORD-20260727-00042");
    }
}
