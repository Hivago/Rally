using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Cart.Abstractions;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.Mappings;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Errors;
using RallyAPI.Orders.Domain.ValueObjects;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.PlaceOrder;

/// <summary>
/// Handler for PlaceOrderCommand.
/// Creates a PENDING order. Payment is handled separately via InitiatePayment + PayU webhook.
/// If the customer has a persisted cart, its items are used instead of the request body items.
/// The cart is cleared after a successful order creation.
/// </summary>
public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly IOrderValidationService _validationService;
    private readonly ICartRepository _cartRepository;
    private readonly ICartCacheService _cartCache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PlaceOrderCommandHandler> _logger;

    private const string DefaultCurrency = "INR";

    public PlaceOrderCommandHandler(
        IOrderRepository orderRepository,
        IOrderNumberGenerator orderNumberGenerator,
        IOrderValidationService validationService,
        ICartRepository cartRepository,
        ICartCacheService cartCache,
        IUnitOfWork unitOfWork,
        ILogger<PlaceOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _orderNumberGenerator = orderNumberGenerator;
        _validationService = validationService;
        _cartRepository = cartRepository;
        _cartCache = cartCache;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OrderDto>> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Creating pending order for Customer {CustomerId}, Restaurant {RestaurantId}",
                command.CustomerId,
                command.Request.RestaurantId);

            // Step 1: Validate customer
            var customerValidation = await _validationService.ValidateCustomerAsync(
                command.CustomerId, cancellationToken);

            if (customerValidation.IsFailure)
            {
                _logger.LogWarning("Customer validation failed: {Error}", customerValidation.Error);
                return Result.Failure<OrderDto>(customerValidation.Error);
            }

            // Step 3: Validate restaurant
            var restaurantValidation = await _validationService.ValidateRestaurantAsync(
                command.Request.RestaurantId, cancellationToken);

            if (restaurantValidation.IsFailure)
            {
                _logger.LogWarning("Restaurant validation failed: {Error}", restaurantValidation.Error);
                return Result.Failure<OrderDto>(restaurantValidation.Error);
            }

            // Step 3b: Validate delivery radius (delivery orders only)
            if (command.Request.FulfillmentType == Domain.Enums.FulfillmentType.Delivery
                && command.Request.DeliveryAddress is not null)
            {
                var addr = command.Request.DeliveryAddress;
                var distanceValidation = await _validationService.ValidateDeliveryDistanceAsync(
                    (double)command.Request.PickupLatitude,
                    (double)command.Request.PickupLongitude,
                    (double)addr.Latitude,
                    (double)addr.Longitude,
                    cancellationToken);

                if (distanceValidation.IsFailure)
                {
                    _logger.LogWarning("Delivery distance validation failed: {Error}", distanceValidation.Error);
                    return Result.Failure<OrderDto>(distanceValidation.Error);
                }
            }

            // Step 4: Resolve order items — prefer cart over request body
            var cart = await _cartRepository.GetByCustomerIdAsync(command.CustomerId, cancellationToken);

            List<(Guid MenuItemId, string ItemName, decimal UnitPrice, int Quantity,
                  string? ItemDescription, string? ImageUrl, string? SpecialInstructions)> resolvedItems;

            if (cart is not null && cart.Items.Any())
            {
                _logger.LogInformation(
                    "Using {Count} item(s) from cart for Customer {CustomerId}",
                    cart.Items.Count, command.CustomerId);

                resolvedItems = cart.Items
                    .Select(i => (i.MenuItemId, i.Name, i.UnitPrice, i.Quantity,
                                  (string?)null, (string?)null, i.SpecialInstructions))
                    .ToList();
            }
            else
            {
                resolvedItems = command.Request.Items
                    .Select(i => (i.MenuItemId, i.ItemName, i.UnitPrice, i.Quantity,
                                  i.ItemDescription, i.ImageUrl, i.SpecialInstructions))
                    .ToList();
            }

            if (resolvedItems.Count == 0)
                return Result.Failure<OrderDto>(OrderErrors.EmptyItems);

            // Step 5: Generate order number
            var orderNumber = await _orderNumberGenerator.GenerateAsync(cancellationToken);

            // Step 6: Create delivery info (only for delivery orders)
            var fulfillmentType = command.Request.FulfillmentType;
            DeliveryInfo? deliveryInfo = null;

            if (fulfillmentType == Domain.Enums.FulfillmentType.Delivery)
            {
                var addr = command.Request.DeliveryAddress!;
                var deliveryAddress = Address.Create(
                    addr.Street,
                    addr.City,
                    addr.Pincode,
                    addr.Latitude,
                    addr.Longitude,
                    addr.Landmark,
                    addr.BuildingName,
                    addr.Floor,
                    addr.ContactPhone,
                    addr.Instructions);

                deliveryInfo = DeliveryInfo.Create(
                    command.Request.PickupLatitude,
                    command.Request.PickupLongitude,
                    command.Request.PickupPincode,
                    deliveryAddress,
                    command.Request.PickupAddress);
            }

            // Step 7: Create order items (from resolved source: cart or request body)
            var orderItems = resolvedItems.Select(item => OrderItem.Create(
                item.MenuItemId,
                item.ItemName,
                Money.FromDecimal(item.UnitPrice, DefaultCurrency),
                item.Quantity,
                item.ItemDescription,
                item.ImageUrl,
                item.SpecialInstructions)).ToList();

            _logger.LogInformation("Received pricing - SubTotal: {Sub}, DeliveryFee: {Del}, Tax: {Tax}",
            command.Request.Pricing.SubTotal,
            command.Request.Pricing.DeliveryFee,
            command.Request.Pricing.Tax);

            // Step 8: Create pricing (already calculated by Cart/Delivery Module)
            var pricing = OrderPricing.Create(
                Money.FromDecimal(command.Request.Pricing.SubTotal, DefaultCurrency),
                Money.FromDecimal(command.Request.Pricing.DeliveryFee, DefaultCurrency),
                Money.FromDecimal(command.Request.Pricing.Tax, DefaultCurrency),
                Money.FromDecimal(command.Request.Pricing.Discount, DefaultCurrency),
                command.Request.Pricing.PackagingFee > 0
                    ? Money.FromDecimal(command.Request.Pricing.PackagingFee, DefaultCurrency)
                    : null,
                command.Request.Pricing.ServiceFee > 0
                    ? Money.FromDecimal(command.Request.Pricing.ServiceFee, DefaultCurrency)
                    : null,
                command.Request.Pricing.Tip > 0
                    ? Money.FromDecimal(command.Request.Pricing.Tip, DefaultCurrency)
                    : null,
                command.Request.Pricing.DiscountCode,
                command.Request.Pricing.DiscountDescription);


            _logger.LogInformation("OrderPricing created - SubTotal: {Sub}, Total: {Total}",
            pricing.SubTotal.Amount, pricing.Total.Amount);

            // Step 9: Create pending order (payment handled separately)
            var order = Order.CreatePendingOrder(
                orderNumber,
                command.CustomerId,
                command.CustomerName,
                command.Request.RestaurantId,
                command.Request.RestaurantName,
                pricing,
                fulfillmentType: fulfillmentType,
                deliveryInfo: deliveryInfo,
                deliveryQuoteId: command.DeliveryQuoteId,
                customerPhone: command.CustomerPhone,
                customerEmail: command.CustomerEmail,
                restaurantPhone: command.Request.RestaurantPhone,
                specialInstructions: command.Request.SpecialInstructions);

            // Step 10: Add items
            order.AddItems(orderItems);

            _logger.LogInformation("Order pricing - SubTotal: {Sub}, DeliveryFee: {Del}, Tax: {Tax}, Total: {Total}",
            order.Pricing.SubTotal.Amount,
            order.Pricing.DeliveryFee.Amount,
            order.Pricing.Tax.Amount,
            order.Pricing.Total.Amount);

            // Step 11: Validate and save (OrderPaidEvent fires only after webhook confirms payment)
            order.ValidateOrder();

            await _orderRepository.AddAsync(order, cancellationToken);

            // Clear cart after successful order — fire-and-forget deletion (non-fatal if it fails)
            // Must invalidate Redis cache too — GetCartQueryHandler reads cache first, so a stale
            // entry would resurface the cart to the customer after checkout.
            if (cart is not null)
            {
                try
                {
                    await _cartRepository.DeleteByCustomerIdAsync(command.CustomerId, cancellationToken);
                    await _cartCache.RemoveAsync(command.CustomerId, cancellationToken);
                }
                catch (Exception cartEx)
                {
                    _logger.LogWarning(cartEx,
                        "Failed to clear cart for Customer {CustomerId} after order {OrderNumber} — non-fatal",
                        command.CustomerId, order.OrderNumber.Value);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Pending order {OrderNumber} created successfully. Total: {Total}. Awaiting payment.",
                order.OrderNumber.Value,
                order.Pricing.Total.ToDisplayString());

            return Result.Success(order.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for Customer {CustomerId}", command.CustomerId);
            return Result.Failure<OrderDto>(OrderErrors.Unexpected(ex.Message));
        }
    }
}