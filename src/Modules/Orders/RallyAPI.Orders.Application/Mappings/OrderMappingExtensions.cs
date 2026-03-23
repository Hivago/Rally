using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Application.Mappings;

/// <summary>
/// Extension methods for mapping domain entities to DTOs.
/// Centralized for consistency and easy modification.
/// </summary>
public static class OrderMappingExtensions
{
    public static OrderDto ToDto(this Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber.Value,

            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            CustomerEmail = order.CustomerEmail,

            RestaurantId = order.RestaurantId,
            RestaurantName = order.RestaurantName,
            RestaurantPhone = order.RestaurantPhone,

            FulfillmentType = order.FulfillmentType,

            Status = order.Status,
            StatusDisplay = order.Status.GetDisplayName(),
            PaymentStatus = order.PaymentStatus,
            PaymentStatusDisplay = order.PaymentStatus.GetDisplayName(),

            Items = order.Items.Select(i => i.ToDto()).ToList(),
            TotalItems = order.Items.Sum(i => i.Quantity),

            Pricing = order.Pricing.ToDto(),
            DeliveryInfo = order.DeliveryInfo?.ToDto(),

            CreatedAt = order.CreatedAt,
            ConfirmedAt = order.ConfirmedAt,
            PreparingAt = order.PreparingAt,
            ReadyAt = order.ReadyAt,
            PickedUpAt = order.PickedUpAt,
            DeliveredAt = order.DeliveredAt,
            CancelledAt = order.CancelledAt,

            CancellationReason = order.CancellationReason,
            CancellationNotes = order.CancellationNotes,

            PaymentId = order.PaymentId,
            PaymentTransactionId = order.PaymentTransactionId,
            DeliveryQuoteId = order.DeliveryQuoteId,
            RejectionReason = order.RejectionReason,
            RejectedAt = order.RejectedAt,

            SpecialInstructions = order.SpecialInstructions,

            CanCancel = order.Status.CanBeCancelled(),
            CanModify = order.Status == OrderStatus.Paid,
            AvailableTransitions = order.GetValidTransitions()
        };
    }

    public static OrderSummaryDto ToSummaryDto(this Order order)
    {
        return new OrderSummaryDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber.Value,
            Status = order.Status,
            StatusDisplay = order.Status.GetDisplayName(),
            RestaurantName = order.RestaurantName,
            TotalItems = order.Items.Sum(i => i.Quantity),
            Total = order.Pricing.Total.Amount,
            TotalDisplay = order.Pricing.Total.ToDisplayString(),
            CreatedAt = order.CreatedAt,
            EstimatedMinutes = order.DeliveryInfo?.EstimatedMinutes,
            EstimatedTimeDisplay = order.DeliveryInfo?.EstimatedMinutes.HasValue == true
                ? $"{order.DeliveryInfo.EstimatedMinutes} mins"
                : null
        };
    }

    public static OrderItemDto ToDto(this OrderItem item)
    {
        return new OrderItemDto
        {
            Id = item.Id,
            MenuItemId = item.MenuItemId,
            ItemName = item.ItemName,
            ItemDescription = item.ItemDescription,
            ImageUrl = item.ImageUrl,
            UnitPrice = item.UnitPrice.Amount,
            Quantity = item.Quantity,
            TotalPrice = item.TotalPrice.Amount,
            SpecialInstructions = item.SpecialInstructions
        };
    }

    public static OrderPricingDto ToDto(this Domain.ValueObjects.OrderPricing pricing)
    {
        return new OrderPricingDto
        {
            SubTotal = pricing.SubTotal.Amount,
            DeliveryFee = pricing.DeliveryFee.Amount,
            Tax = pricing.Tax.Amount,
            Discount = pricing.Discount.Amount,
            PackagingFee = pricing.PackagingFee.Amount,
            ServiceFee = pricing.ServiceFee.Amount,
            Tip = pricing.Tip.Amount,
            Total = pricing.Total.Amount,
            Currency = pricing.SubTotal.Currency,
            DiscountCode = pricing.DiscountCode,
            DiscountDescription = pricing.DiscountDescription,

            SubTotalDisplay = pricing.SubTotal.ToDisplayString(),
            DeliveryFeeDisplay = pricing.DeliveryFee.ToDisplayString(),
            TaxDisplay = pricing.Tax.ToDisplayString(),
            DiscountDisplay = pricing.Discount.ToDisplayString(),
            TotalDisplay = pricing.Total.ToDisplayString()
        };
    }

    public static DeliveryInfoDto ToDto(this DeliveryInfo deliveryInfo)
    {
        return new DeliveryInfoDto
        {
            PickupLatitude = deliveryInfo.PickupLocation.Latitude,
            PickupLongitude = deliveryInfo.PickupLocation.Longitude,
            PickupPincode = deliveryInfo.PickupPincode,
            PickupAddress = deliveryInfo.PickupAddress,

            DeliveryAddress = deliveryInfo.DeliveryAddress.ToDto(),

            QuoteId = deliveryInfo.QuoteId,
            ProviderName = deliveryInfo.ProviderName,
            QuotedDeliveryFee = deliveryInfo.QuotedDeliveryFee?.Amount,
            EstimatedMinutes = deliveryInfo.EstimatedMinutes,
            QuotedAt = deliveryInfo.QuotedAt,

            RiderId = deliveryInfo.RiderId,
            RiderName = deliveryInfo.RiderName,
            RiderPhone = deliveryInfo.RiderPhone,
            TrackingUrl = deliveryInfo.TrackingUrl,

            AssignedAt = deliveryInfo.AssignedAt,
            PickedUpAt = deliveryInfo.PickedUpAt,
            DeliveredAt = deliveryInfo.DeliveredAt,

            DistanceKm = deliveryInfo.DistanceKm,
            DistanceDisplay = deliveryInfo.DistanceKm.HasValue
                ? $"{deliveryInfo.DistanceKm:F1} km"
                : null,
            EstimatedTimeDisplay = deliveryInfo.EstimatedMinutes.HasValue
                ? $"{deliveryInfo.EstimatedMinutes} mins"
                : null
        };
    }

    public static AddressDto ToDto(this Domain.ValueObjects.Address address)
    {
        return new AddressDto
        {
            Street = address.Street,
            City = address.City,
            Pincode = address.Pincode,
            Latitude = address.Latitude,
            Longitude = address.Longitude,
            Landmark = address.Landmark,
            BuildingName = address.BuildingName,
            Floor = address.Floor,
            ContactPhone = address.ContactPhone,
            Instructions = address.Instructions,
            FormattedAddress = address.GetFormattedAddress()
        };
    }
}