using FluentValidation;
using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Application.RestaurantLeads.Commands.SubmitLead;

public sealed class SubmitRestaurantLeadCommandValidator
    : AbstractValidator<SubmitRestaurantLeadCommand>
{
    public SubmitRestaurantLeadCommandValidator()
    {
        RuleFor(x => x.RestaurantName)
            .NotEmpty().WithMessage("Restaurant name is required.")
            .MaximumLength(200).WithMessage("Restaurant name must not exceed 200 characters.");

        RuleFor(x => x.OwnerName)
            .NotEmpty().WithMessage("Owner name is required.")
            .MaximumLength(200).WithMessage("Owner name must not exceed 200 characters.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.")
            .Matches(@"^\d{10}$").WithMessage("Phone must be a 10-digit number.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters.");

        RuleFor(x => x.DailyOrders)
            .Must(v => RestaurantLead.AllowedDailyOrderBuckets.Contains(v))
            .WithMessage($"Daily orders must be one of: {string.Join(", ", RestaurantLead.AllowedDailyOrderBuckets)}.");

        RuleFor(x => x.Source)
            .MaximumLength(100).WithMessage("Source must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Source));
    }
}
