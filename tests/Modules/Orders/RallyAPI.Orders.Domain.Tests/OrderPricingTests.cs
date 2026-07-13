using FluentAssertions;
using RallyAPI.Orders.Domain.ValueObjects;
using Xunit;

namespace RallyAPI.Orders.Domain.Tests;

public class OrderPricingTests
{
    private static Money Inr(decimal v) => Money.FromDecimal(v, "INR");

    [Fact]
    public void Create_IncludesPlatformFeeAndServiceGstInTotal()
    {
        var pricing = OrderPricing.Create(
            subTotal: Inr(200m),
            deliveryFee: Inr(20m),
            tax: Inr(10m),          // food GST (5%)
            discount: Inr(0m),
            platformFee: Inr(10m),
            serviceGst: Inr(5.40m)); // 18% of (20 + 10)

        pricing.PlatformFee.Amount.Should().Be(10m);
        pricing.ServiceGst.Amount.Should().Be(5.40m);
        // 200 + 20 + 10 (food tax) + 10 (platform) + 5.40 (service gst) = 245.40
        pricing.Total.Amount.Should().Be(245.40m);
    }

    [Fact]
    public void Create_DefaultsPlatformFeeAndServiceGstToZero_TotalUnchanged()
    {
        // Backward compatibility: an old caller that doesn't pass platform/GST gets the same
        // total it always did (no phantom charges).
        var pricing = OrderPricing.Create(
            subTotal: Inr(200m),
            deliveryFee: Inr(30m),
            tax: Inr(10m),
            discount: Inr(0m));

        pricing.PlatformFee.Amount.Should().Be(0m);
        pricing.ServiceGst.Amount.Should().Be(0m);
        pricing.Total.Amount.Should().Be(240m);
    }
}
