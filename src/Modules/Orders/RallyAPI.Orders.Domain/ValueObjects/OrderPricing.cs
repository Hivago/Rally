using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Orders.Domain.ValueObjects;

/// <summary>
/// Encapsulates all pricing components of an order.
/// Keeps pricing logic centralized and easy to modify.
/// </summary>
public sealed class OrderPricing : ValueObject
{
    public Money SubTotal { get; }
    public Money DeliveryFee { get; }
    public Money Tax { get; }
    public Money Discount { get; }
    public Money Total { get; }

    // Breakdown for transparency
    public Money PackagingFee { get; }
    public Money ServiceFee { get; }
    public Money Tip { get; }
    public Money TotalAmount { get; }

    // Discount details
    public string? DiscountCode { get; }
    public string? DiscountDescription { get; }

    private OrderPricing(
        Money subTotal,
        Money deliveryFee,
        Money tax,
        Money discount,
        Money packagingFee,
        Money serviceFee,
        Money tip,
        string? discountCode,
        string? discountDescription)
    {
        SubTotal = subTotal;
        DeliveryFee = deliveryFee;
        Tax = tax;
        Discount = discount;
        PackagingFee = packagingFee;
        ServiceFee = serviceFee;
        Tip = tip;
        DiscountCode = discountCode;
        DiscountDescription = discountDescription;

        // Calculate total
        Total = subTotal + deliveryFee + tax + packagingFee + serviceFee + tip - discount;
    }

    public static OrderPricing Create(
        Money subTotal,
        Money deliveryFee,
        Money tax,
        Money discount,
        Money? packagingFee = null,
        Money? serviceFee = null,
        Money? tip = null,
        string? discountCode = null,
        string? discountDescription = null)
    {
        var currency = subTotal.Currency;

        return new OrderPricing(
            subTotal,
            deliveryFee,
            tax,
            discount,
            packagingFee ?? Money.Zero(currency),
            serviceFee ?? Money.Zero(currency),
            tip ?? Money.Zero(currency),
            discountCode,
            discountDescription);
    }

    /// <summary>
    /// Creates simple pricing (MVP - just subtotal, delivery, tax)
    /// </summary>
    public static OrderPricing CreateSimple(
        decimal subTotal,
        decimal deliveryFee,
        decimal taxRate = 0.05m,  // 5% default tax
        decimal discount = 0m,
        string currency = "INR")
    {
        var subTotalMoney = Money.FromDecimal(subTotal, currency);
        var deliveryFeeMoney = Money.FromDecimal(deliveryFee, currency);
        var taxMoney = Money.FromDecimal(subTotal * taxRate, currency);
        var discountMoney = Money.FromDecimal(discount, currency);

        return new OrderPricing(
            subTotalMoney,
            deliveryFeeMoney,
            taxMoney,
            discountMoney,
            Money.Zero(currency),
            Money.Zero(currency),
            Money.Zero(currency),
            null,
            null);
    }

    /// <summary>
    /// Creates updated pricing with new delivery fee (e.g., after re-quote)
    /// </summary>
    public OrderPricing WithDeliveryFee(Money newDeliveryFee)
    {
        return new OrderPricing(
            SubTotal,
            newDeliveryFee,
            Tax,
            Discount,
            PackagingFee,
            ServiceFee,
            Tip,
            DiscountCode,
            DiscountDescription);
    }

    /// <summary>
    /// Creates updated pricing with tip added
    /// </summary>
    public OrderPricing WithTip(Money tip)
    {
        return new OrderPricing(
            SubTotal,
            DeliveryFee,
            Tax,
            Discount,
            PackagingFee,
            ServiceFee,
            tip,
            DiscountCode,
            DiscountDescription);
    }

    /// <summary>
    /// Creates updated pricing with discount applied
    /// </summary>
    public OrderPricing WithDiscount(Money discount, string? code = null, string? description = null)
    {
        return new OrderPricing(
            SubTotal,
            DeliveryFee,
            Tax,
            discount,
            PackagingFee,
            ServiceFee,
            Tip,
            code ?? DiscountCode,
            description ?? DiscountDescription);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return SubTotal;
        yield return DeliveryFee;
        yield return Tax;
        yield return Discount;
        yield return PackagingFee;
        yield return ServiceFee;
        yield return Tip;
        yield return Total;
    }

    public override string ToString() =>
        $"SubTotal: {SubTotal}, Delivery: {DeliveryFee}, Tax: {Tax}, Discount: {Discount}, Total: {Total}";
}