using System.Text.Json;
using FluentAssertions;
using MediatR;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;
using Xunit;

namespace RallyAPI.Orders.Application.Tests.Outbox;

/// <summary>
/// Guards the contract shared by OutboxWriter (serialize: assembly-qualified type + JSON)
/// and OutboxProcessor (resolve type, deserialize, publish as INotification). If the
/// integration event's constructor/properties drift out of sync, this breaks loudly here
/// instead of silently dead-lettering messages in production.
/// </summary>
public class OutboxSerializationTests
{
    [Fact]
    public void OrderConfirmedIntegrationEvent_RoundTripsThroughOutboxSerialization()
    {
        var original = new OrderConfirmedIntegrationEvent(
            orderId: Guid.NewGuid(),
            orderNumber: "ORD-2026-0042",
            restaurantId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            restaurantName: "Dosa Corner",
            restaurantPhone: "+919900000000",
            pickupAddress: "12 MG Road",
            pickupLatitude: 12.9352,
            pickupLongitude: 77.6245,
            pickupPincode: "560095",
            customerName: "Priya",
            customerPhone: "+919811111111",
            dropAddress: "42 Brigade Road",
            dropLatitude: 12.9716,
            dropLongitude: 77.5946,
            dropPincode: "560025",
            itemCount: 3,
            totalAmount: 350.50m,
            deliveryInstructions: "Ring the bell",
            quoteId: Guid.NewGuid(),
            confirmedAt: DateTime.UtcNow,
            isPickupOrder: false);

        // Mirror OutboxWriter.
        var type = original.GetType();
        var typeName = type.AssemblyQualifiedName!;
        var json = JsonSerializer.Serialize(original, type);

        // Mirror OutboxProcessor.
        var resolvedType = Type.GetType(typeName);
        resolvedType.Should().Be(type);

        var deserialized = JsonSerializer.Deserialize(json, resolvedType!);
        deserialized.Should().BeOfType<OrderConfirmedIntegrationEvent>();
        deserialized.Should().BeAssignableTo<INotification>();

        var roundTripped = (OrderConfirmedIntegrationEvent)deserialized!;
        roundTripped.OrderId.Should().Be(original.OrderId);
        roundTripped.OrderNumber.Should().Be(original.OrderNumber);
        roundTripped.RestaurantId.Should().Be(original.RestaurantId);
        roundTripped.CustomerId.Should().Be(original.CustomerId);
        roundTripped.DropPincode.Should().Be("560025");
        roundTripped.PickupPincode.Should().Be("560095");
        roundTripped.ItemCount.Should().Be(3);
        roundTripped.TotalAmount.Should().Be(350.50m);
        roundTripped.DeliveryInstructions.Should().Be("Ring the bell");
        roundTripped.QuoteId.Should().Be(original.QuoteId);
        roundTripped.IsPickupOrder.Should().BeFalse();
    }
}
