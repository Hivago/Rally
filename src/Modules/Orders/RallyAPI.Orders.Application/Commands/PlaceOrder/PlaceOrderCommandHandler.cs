using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
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
/// Creates order AFTER payment is successful.
/// Delivery quote is already obtained via Delivery Module.
/// </summary>
public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly IOrderValidationService _validationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PlaceOrderCommandHandler> _logger;

    private const string DefaultCurrency = "INR";

    public PlaceOrderCommandHandler(
        IOrderRepository orderRepository,
        IOrderNumberGenerator orderNumberGenerator,
        IOrderValidationService validationService,
        IUnitOfWork unitOfWork,
        ILogger<PlaceOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _orderNumberGenerator = orderNumberGenerator;
        _validationService = validationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OrderDto>> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Creating paid order for Customer {CustomerId}, Restaurant {RestaurantId}, Payment {PaymentId}",
                command.CustomerId,
                command.Request.RestaurantId,
                command.PaymentId);

            // Step 1: Validate payment ID exists
            if (string.IsNullOrWhiteSpace(command.PaymentId))
            {
                return Result.Failure<OrderDto>(OrderErrors.PaymentIdRequired);
            }

            // Step 2: Validate customer
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

            // Step 4: Generate order number
            var orderNumber = await _orderNumberGenerator.GenerateAsync(cancellationToken);

            // Step 5: Create delivery address
            var deliveryAddress = Address.Create(
                command.Request.DeliveryAddress.Street,
                command.Request.DeliveryAddress.City,
                command.Request.DeliveryAddress.Pincode,
                command.Request.DeliveryAddress.Latitude,
                command.Request.DeliveryAddress.Longitude,
                command.Request.DeliveryAddress.Landmark,
                command.Request.DeliveryAddress.BuildingName,
                command.Request.DeliveryAddress.Floor,
                command.Request.DeliveryAddress.ContactPhone,
                command.Request.DeliveryAddress.Instructions);

            // Step 6: Create delivery info
            var deliveryInfo = DeliveryInfo.Create(
                command.Request.PickupLatitude,
                command.Request.PickupLongitude,
                command.Request.PickupPincode,
                deliveryAddress,
                command.Request.PickupAddress);

            // Step 7: Create order items
            var orderItems = command.Request.Items.Select(item => OrderItem.Create(
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

            // Step 9: Create paid order
            var order = Order.CreatePaidOrder(
                orderNumber,
                command.CustomerId,
                command.CustomerName,
                command.Request.RestaurantId,
                command.Request.RestaurantName,
                deliveryInfo,
                pricing,
                command.PaymentId,
                command.PaymentTransactionId,
                command.DeliveryQuoteId,
                command.CustomerPhone,
                command.CustomerEmail,
                command.Request.RestaurantPhone,
                command.Request.SpecialInstructions);

            // Step 10: Add items
            order.AddItems(orderItems);

            _logger.LogInformation("Order pricing - SubTotal: {Sub}, DeliveryFee: {Del}, Tax: {Tax}, Total: {Total}",
            order.Pricing.SubTotal.Amount,
            order.Pricing.DeliveryFee.Amount,
            order.Pricing.Tax.Amount,
            order.Pricing.Total.Amount);

            // Step 11: Finalize and save
            order.FinalizeOrder();

            await _orderRepository.AddAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Paid order {OrderNumber} created successfully. Total: {Total}. Awaiting restaurant response.",
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