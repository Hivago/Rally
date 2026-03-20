using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;
using RallyAPI.Pricing.Application.Services;
using RallyAPI.Pricing.Domain.Abstractions;
using RallyAPI.Pricing.Domain.Entities;
using RallyAPI.Pricing.Domain.Enums;
using RallyAPI.Pricing.Domain.Repositories;
using RallyAPI.Pricing.Domain.ValueObjects;

namespace RallyAPI.Pricing.Application.Tests;

public class PricingEngineTests
{
    private readonly IPricingConfigRepository _configRepository;
    private readonly ILogger<PricingEngine> _logger;

    public PricingEngineTests()
    {
        _configRepository = Substitute.For<IPricingConfigRepository>();
        _logger = Substitute.For<ILogger<PricingEngine>>();

        // Default: no min/max caps
        _configRepository.GetActiveBaseFeeConfigAsync(Arg.Any<CancellationToken>())
            .Returns((BaseFeeConfig?)null);
    }

    [Fact]
    public async Task CalculateDeliveryFeeAsync_WhenNoRulesApply_ShouldReturnZeroFee()
    {
        var engine = new PricingEngine(Array.Empty<IPricingRule>(), _configRepository, _logger);
        var context = BuildContext();

        var result = await engine.CalculateDeliveryFeeAsync(context);

        result.BaseFee.Should().Be(0);
        result.FinalFee.Should().Be(0);
    }

    [Fact]
    public async Task CalculateDeliveryFeeAsync_WithBaseFeeRule_ShouldReturnBaseFee()
    {
        var baseFeeRule = BuildRule("BaseFee", ModificationType.Flat, 50m, priority: 1);
        var engine = new PricingEngine(new[] { baseFeeRule }, _configRepository, _logger);
        var context = BuildContext();

        var result = await engine.CalculateDeliveryFeeAsync(context);

        result.BaseFee.Should().Be(50m);
        result.FinalFee.Should().Be(50m);
    }

    [Fact]
    public async Task CalculateDeliveryFeeAsync_WithBaseFeeAndFlatSurcharge_ShouldSumBoth()
    {
        var baseFeeRule = BuildRule("BaseFee", ModificationType.Flat, 50m, priority: 1);
        var distanceRule = BuildRule("DistanceFee", ModificationType.Flat, 20m, priority: 2);
        var engine = new PricingEngine(new[] { baseFeeRule, distanceRule }, _configRepository, _logger);
        var context = BuildContext();

        var result = await engine.CalculateDeliveryFeeAsync(context);

        result.BaseFee.Should().Be(50m);
        result.FinalFee.Should().Be(70m); // 50 + 20
    }

    [Fact]
    public async Task CalculateDeliveryFeeAsync_WithPercentageSurcharge_ShouldAddPercentageOfBaseFee()
    {
        var baseFeeRule = BuildRule("BaseFee", ModificationType.Flat, 100m, priority: 1);
        var percentageRule = BuildRule("RainSurcharge", ModificationType.Percentage, 10m, priority: 2); // 10% of baseFee
        var engine = new PricingEngine(new[] { baseFeeRule, percentageRule }, _configRepository, _logger);
        var context = BuildContext();

        var result = await engine.CalculateDeliveryFeeAsync(context);

        result.FinalFee.Should().Be(110m); // 100 + 10% of 100
    }

    [Fact]
    public async Task CalculateDeliveryFeeAsync_WithMultiplierSurge_ShouldMultiplyTotal()
    {
        var baseFeeRule = BuildRule("BaseFee", ModificationType.Flat, 100m, priority: 1);
        var surgeRule = BuildRule("PeakSurge", ModificationType.Multiplier, 1.5m, priority: 2);
        var engine = new PricingEngine(new[] { baseFeeRule, surgeRule }, _configRepository, _logger);
        var context = BuildContext();

        var result = await engine.CalculateDeliveryFeeAsync(context);

        result.FinalFee.Should().Be(150m); // 100 * 1.5
        result.SurgeMultiplier.Should().Be(1.5m);
        result.PrimarySurgeReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CalculateDeliveryFeeAsync_WhenFeeIsBelowMinimum_ShouldCapAtMinimum()
    {
        var baseFeeRule = BuildRule("BaseFee", ModificationType.Flat, 20m, priority: 1);
        var config = BaseFeeConfig.Create(amount: 0m, minFee: 40m, maxFee: null);
        _configRepository.GetActiveBaseFeeConfigAsync(Arg.Any<CancellationToken>()).Returns(config);

        var engine = new PricingEngine(new[] { baseFeeRule }, _configRepository, _logger);
        var context = BuildContext();

        var result = await engine.CalculateDeliveryFeeAsync(context);

        result.FinalFee.Should().Be(40m);
    }

    [Fact]
    public async Task CalculateDeliveryFeeAsync_WhenFeeExceedsMaximum_ShouldCapAtMaximum()
    {
        var baseFeeRule = BuildRule("BaseFee", ModificationType.Flat, 100m, priority: 1);
        var surgeRule = BuildRule("PeakSurge", ModificationType.Multiplier, 3m, priority: 2);
        var config = BaseFeeConfig.Create(amount: 0m, minFee: null, maxFee: 200m);
        _configRepository.GetActiveBaseFeeConfigAsync(Arg.Any<CancellationToken>()).Returns(config);

        var engine = new PricingEngine(new[] { baseFeeRule, surgeRule }, _configRepository, _logger);
        var context = BuildContext();

        var result = await engine.CalculateDeliveryFeeAsync(context);

        result.FinalFee.Should().Be(200m); // capped at 200, would have been 300
    }

    [Fact]
    public async Task CalculateDeliveryFeeAsync_WhenRuleIsDisabled_ShouldSkipIt()
    {
        var baseFeeRule = BuildRule("BaseFee", ModificationType.Flat, 50m, priority: 1);
        var disabledRule = BuildRule("SpecialSurge", ModificationType.Flat, 999m, priority: 2, isEnabled: false);
        var engine = new PricingEngine(new[] { baseFeeRule, disabledRule }, _configRepository, _logger);
        var context = BuildContext();

        var result = await engine.CalculateDeliveryFeeAsync(context);

        result.FinalFee.Should().Be(50m);
    }

    [Fact]
    public async Task CalculateDeliveryFeeAsync_WhenRuleThrows_ShouldSwallowExceptionAndContinue()
    {
        var baseFeeRule = BuildRule("BaseFee", ModificationType.Flat, 50m, priority: 1);

        var faultyRule = Substitute.For<IPricingRule>();
        faultyRule.IsEnabled.Returns(true);
        faultyRule.Priority.Returns(2);
        faultyRule.RuleName.Returns("FaultyRule");
        faultyRule.AppliesAsync(Arg.Any<PricingContext>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("Boom!"));

        var engine = new PricingEngine(new[] { baseFeeRule, faultyRule }, _configRepository, _logger);
        var context = BuildContext();

        var act = () => engine.CalculateDeliveryFeeAsync(context);

        await act.Should().NotThrowAsync();
        var result = await engine.CalculateDeliveryFeeAsync(context);
        result.FinalFee.Should().Be(50m); // BaseFee rule still applied
    }

    #region Helpers

    private static PricingContext BuildContext() => new()
    {
        RestaurantLatitude = 12.935,
        RestaurantLongitude = 77.624,
        CustomerLatitude = 12.971,
        CustomerLongitude = 77.594,
        OrderTime = DateTime.UtcNow,
        DayOfWeek = DayOfWeek.Monday,
        OrderSubtotal = 300m,
        ItemCount = 3,
        RestaurantId = Guid.NewGuid()
    };

    private static IPricingRule BuildRule(
        string name,
        ModificationType type,
        decimal amount,
        int priority,
        bool isEnabled = true)
    {
        var rule = Substitute.For<IPricingRule>();
        rule.RuleName.Returns(name);
        rule.Priority.Returns(priority);
        rule.IsEnabled.Returns(isEnabled);
        rule.AppliesAsync(Arg.Any<PricingContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        rule.CalculateAsync(Arg.Any<PricingContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PriceModification?>(
                new PriceModification(name, $"{name} description", amount, type, priority)));
        return rule;
    }

    #endregion
}
