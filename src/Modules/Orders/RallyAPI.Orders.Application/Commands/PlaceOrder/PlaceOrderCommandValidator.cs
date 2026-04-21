//using FluentValidation;

//namespace RallyAPI.Orders.Application.Commands.PlaceOrder;

///// <summary>
///// Validator for PlaceOrderCommand.
///// </summary>
//public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
//{
//    public PlaceOrderCommandValidator()
//    {
//        RuleFor(x => x.CustomerId)
//            .NotEmpty()
//            .WithMessage("Customer ID is required");

//        RuleFor(x => x.CustomerName)
//            .NotEmpty()
//            .MaximumLength(200)
//            .WithMessage("Customer name is required");

//        RuleFor(x => x.Request.RestaurantId)
//            .NotEmpty()
//            .WithMessage("Restaurant ID is required");

//        RuleFor(x => x.Request.RestaurantName)
//            .NotEmpty()
//            .MaximumLength(200)
//            .WithMessage("Restaurant name is required");

//        RuleFor(x => x.Request.PickupPincode)
//            .NotEmpty()
//            .MaximumLength(10)
//            .WithMessage("Pickup pincode is required");

//        RuleFor(x => x.Request.PickupLatitude)
//            .InclusiveBetween(-90, 90)
//            .WithMessage("Invalid pickup latitude");

//        RuleFor(x => x.Request.PickupLongitude)
//            .InclusiveBetween(-180, 180)
//            .WithMessage("Invalid pickup longitude");

//        RuleFor(x => x.Request.Items)
//            .NotEmpty()
//            .WithMessage("Order must contain at least one item");

//        RuleForEach(x => x.Request.Items).ChildRules(item =>
//        {
//            item.RuleFor(i => i.MenuItemId)
//                .NotEmpty()
//                .WithMessage("Menu item ID is required");

//            item.RuleFor(i => i.ItemName)
//                .NotEmpty()
//                .MaximumLength(200)
//                .WithMessage("Item name is required");

//            item.RuleFor(i => i.Quantity)
//                .GreaterThan(0)
//                .WithMessage("Quantity must be greater than zero");

//            item.RuleFor(i => i.UnitPrice)
//                .GreaterThanOrEqualTo(0)
//                .WithMessage("Unit price cannot be negative");
//        });

//        RuleFor(x => x.Request.DeliveryAddress).ChildRules(addr =>
//        {
//            addr.RuleFor(a => a.Street)
//                .NotEmpty()
//                .MaximumLength(255)
//                .WithMessage("Delivery street is required");

//            addr.RuleFor(a => a.City)
//                .NotEmpty()
//                .MaximumLength(100)
//                .WithMessage("Delivery city is required");

//            addr.RuleFor(a => a.Pincode)
//                .NotEmpty()
//                .MaximumLength(10)
//                .WithMessage("Delivery pincode is required");

//            addr.RuleFor(a => a.Latitude)
//                .InclusiveBetween(-90, 90)
//                .WithMessage("Invalid delivery latitude");

//            addr.RuleFor(a => a.Longitude)
//                .InclusiveBetween(-180, 180)
//                .WithMessage("Invalid delivery longitude");
//        });

//        //RuleFor(x => x.Request.TipAmount)
//        //    .GreaterThanOrEqualTo(0)
//        //    .When(x => x.Request.TipAmount.HasValue)
//        //    .WithMessage("Tip amount cannot be negative");


//        // ADD THIS INSTEAD (if you want to validate Pricing.Tip):
//        RuleFor(x => x.Request.Pricing.Tip)
//            .GreaterThanOrEqualTo(0)
//            .WithMessage("Tip amount cannot be negative");

//    }
//}

using FluentValidation;
using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Application.Commands.PlaceOrder;

/// <summary>
/// Validator for PlaceOrderCommand.
/// </summary>
public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");

        RuleFor(x => x.CustomerName)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Customer name is required");

        RuleFor(x => x.Request.FulfillmentType)
            .IsInEnum()
            .WithMessage("Invalid fulfillment type");

        RuleFor(x => x.Request.RestaurantId)
            .NotEmpty()
            .WithMessage("Restaurant ID is required");

        RuleFor(x => x.Request.RestaurantName)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Restaurant name is required");

        RuleFor(x => x.Request.PickupPincode)
            .NotEmpty()
            .MaximumLength(10)
            .WithMessage("Pickup pincode is required");

        RuleFor(x => x.Request.PickupLatitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Invalid pickup latitude");

        RuleFor(x => x.Request.PickupLongitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Invalid pickup longitude");

        RuleFor(x => x.Request.Items)
            .NotEmpty()
            .WithMessage("Order must contain at least one item");

        RuleForEach(x => x.Request.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MenuItemId)
                .NotEmpty()
                .WithMessage("Menu item ID is required");

            item.RuleFor(i => i.ItemName)
                .NotEmpty()
                .MaximumLength(200)
                .WithMessage("Item name is required");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .WithMessage("Quantity must be greater than zero");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Unit price cannot be negative");
        });

        // Delivery address is required only for delivery orders
        RuleFor(x => x.Request.DeliveryAddress)
            .NotNull()
            .WithMessage("Delivery address is required for delivery orders")
            .When(x => x.Request.FulfillmentType == FulfillmentType.Delivery);

        When(x => x.Request.FulfillmentType == FulfillmentType.Delivery
                && x.Request.DeliveryAddress is not null, () =>
        {
            RuleFor(x => x.Request.DeliveryAddress!.Street)
                .NotEmpty()
                .MaximumLength(255)
                .WithMessage("Delivery street is required");

            RuleFor(x => x.Request.DeliveryAddress!.City)
                .NotEmpty()
                .MaximumLength(100)
                .WithMessage("Delivery city is required");

            RuleFor(x => x.Request.DeliveryAddress!.Pincode)
                .NotEmpty()
                .MaximumLength(10)
                .WithMessage("Delivery pincode is required");

            RuleFor(x => x.Request.DeliveryAddress!.Latitude)
                .InclusiveBetween(-90, 90)
                .WithMessage("Invalid delivery latitude");

            RuleFor(x => x.Request.DeliveryAddress!.Longitude)
                .InclusiveBetween(-180, 180)
                .WithMessage("Invalid delivery longitude");
        });

        // Pricing validation
        RuleFor(x => x.Request.Pricing.SubTotal)
            .GreaterThan(0)
            .WithMessage("Subtotal must be greater than zero");

        RuleFor(x => x.Request.Pricing.Tip)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Tip cannot be negative");
    }
}