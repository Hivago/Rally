using FluentAssertions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using Xunit;

namespace RallyAPI.Orders.Domain.Tests;

public class PayoutLedgerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OutletId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private const string OrderNumber = "ORD-20260713-00001";

    [Fact]
    public void Create_WithFlatFee_ComputesPhase2Formula()
    {
        // Per spec acceptance: orderAmount=500, commissionFlatFee=30
        //   gst_on_food   = 500 * 0.05 = 25.00
        //   commission    = 30.00
        //   commission_gst = 30 * 0.18 = 5.40
        //   tds           = 500 * 0.01 = 5.00   (on gross subtotal — Phase 2 fix)
        //   net           = 500 - 30 - 5.40 - 5.00 = 459.60
        var ledger = PayoutLedger.Create(
            ownerId: OwnerId,
            outletId: OutletId,
            orderId: OrderId,
            orderNumber: OrderNumber,
            orderAmount: 500m,
            commissionFlatFee: 30m);

        ledger.OrderAmount.Should().Be(500m);
        ledger.GstAmount.Should().Be(25.00m);
        ledger.CommissionFlatFee.Should().Be(30.00m);
        ledger.CommissionAmount.Should().Be(30.00m);
        ledger.CommissionGst.Should().Be(5.40m);
        ledger.TdsAmount.Should().Be(5.00m);
        ledger.NetAmount.Should().Be(459.60m);
        ledger.CommissionPercentage.Should().Be(0m); // Legacy field, zero for Phase 2 rows
        ledger.Status.Should().Be(PayoutLedgerStatus.Pending);
        ledger.Currency.Should().Be("INR");
        ledger.PayoutId.Should().BeNull();
    }

    [Fact]
    public void Create_RoundsCommissionGstToTwoDecimals()
    {
        // commissionFlatFee = 33 → commissionGst = 33 * 0.18 = 5.94 (already 2dp, exact)
        // pick a value that requires rounding: 35 * 0.18 = 6.30 (exact too)
        // Use 27.50 → commissionGst = 27.50 * 0.18 = 4.95 (exact)
        // Use 33.33 → commissionGst = 33.33 * 0.18 = 5.9994 → 6.00
        var ledger = PayoutLedger.Create(OwnerId, OutletId, OrderId, OrderNumber, 500m, 33.33m);

        ledger.CommissionAmount.Should().Be(33.33m);
        ledger.CommissionGst.Should().Be(6.00m);
    }

    [Fact]
    public void Create_WithZeroFlatFee_ProducesZeroCommissionAndCommissionGst()
    {
        var ledger = PayoutLedger.Create(OwnerId, OutletId, OrderId, OrderNumber, 500m, 0m);

        ledger.CommissionAmount.Should().Be(0m);
        ledger.CommissionGst.Should().Be(0m);
        // net = 500 - 0 - 0 - 5.00 = 495.00
        ledger.NetAmount.Should().Be(495.00m);
    }

    [Fact]
    public void Create_WithEmptyOwnerId_Throws()
    {
        var act = () => PayoutLedger.Create(Guid.Empty, OutletId, OrderId, OrderNumber, 500m, 30m);
        act.Should().Throw<ArgumentException>().WithParameterName("ownerId");
    }

    [Fact]
    public void Create_WithEmptyOutletId_Throws()
    {
        var act = () => PayoutLedger.Create(OwnerId, Guid.Empty, OrderId, OrderNumber, 500m, 30m);
        act.Should().Throw<ArgumentException>().WithParameterName("outletId");
    }

    [Fact]
    public void Create_WithEmptyOrderId_Throws()
    {
        var act = () => PayoutLedger.Create(OwnerId, OutletId, Guid.Empty, OrderNumber, 500m, 30m);
        act.Should().Throw<ArgumentException>().WithParameterName("orderId");
    }

    [Fact]
    public void Create_WithEmptyOrderNumber_Throws()
    {
        var act = () => PayoutLedger.Create(OwnerId, OutletId, OrderId, " ", 500m, 30m);
        act.Should().Throw<ArgumentException>().WithParameterName("orderNumber");
    }

    [Fact]
    public void Create_WithNonPositiveOrderAmount_Throws()
    {
        var act = () => PayoutLedger.Create(OwnerId, OutletId, OrderId, OrderNumber, 0m, 30m);
        act.Should().Throw<ArgumentException>().WithParameterName("orderAmount");
    }

    [Fact]
    public void Create_WithNegativeFlatFee_Throws()
    {
        var act = () => PayoutLedger.Create(OwnerId, OutletId, OrderId, OrderNumber, 500m, -1m);
        act.Should().Throw<ArgumentException>().WithParameterName("commissionFlatFee");
    }

    [Fact]
    public void AssignToPayout_FromPending_TransitionsToBatched()
    {
        var ledger = PayoutLedger.Create(OwnerId, OutletId, OrderId, OrderNumber, 500m, 30m);
        var payoutId = Guid.NewGuid();

        ledger.AssignToPayout(payoutId);

        ledger.Status.Should().Be(PayoutLedgerStatus.Batched);
        ledger.PayoutId.Should().Be(payoutId);
    }

    [Fact]
    public void AssignToPayout_FromBatched_Throws()
    {
        var ledger = PayoutLedger.Create(OwnerId, OutletId, OrderId, OrderNumber, 500m, 30m);
        ledger.AssignToPayout(Guid.NewGuid());

        var act = () => ledger.AssignToPayout(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsPaidOut_WithoutBatching_Throws()
    {
        var ledger = PayoutLedger.Create(OwnerId, OutletId, OrderId, OrderNumber, 500m, 30m);

        var act = () => ledger.MarkAsPaidOut();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsPaidOut_FromBatched_TransitionsToPaidOut()
    {
        var ledger = PayoutLedger.Create(OwnerId, OutletId, OrderId, OrderNumber, 500m, 30m);
        ledger.AssignToPayout(Guid.NewGuid());

        ledger.MarkAsPaidOut();

        ledger.Status.Should().Be(PayoutLedgerStatus.PaidOut);
    }
}
