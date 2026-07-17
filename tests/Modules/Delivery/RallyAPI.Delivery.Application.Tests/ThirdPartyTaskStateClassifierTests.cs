using FluentAssertions;
using RallyAPI.Delivery.Application.Services;
using Xunit;

namespace RallyAPI.Delivery.Application.Tests;

/// <summary>
/// This classifier decides whether we cancel a live 3PL booking. Getting it wrong costs money and
/// strands a real rider mid-delivery, so the bias is deliberate and asymmetric:
/// only a positively-confirmed "still searching" may lead to a cancel. Everything else — assigned,
/// dead, blank, or unrecognised — must NOT be reported as Searching.
///
/// Real incident (2026-07-17, staging ORD-20260717-00279): our status said Searching3PL because no
/// webhook arrived, so the sweep cancelled task mfnb_fx6fsryz and re-booked. ProRouting's own status
/// for that task was "Order-delivered" by rider SUNIL DINESH, billed at ₹71.98.
/// </summary>
public class ThirdPartyTaskStateClassifierTests
{
    [Theory]
    // The exact state ProRouting returned for the delivered order we cancelled.
    [InlineData("Order-delivered")]
    [InlineData("Agent-assigned")]
    [InlineData("agent_assigned")]
    [InlineData("At-pickup")]
    [InlineData("Order-picked-up")]
    [InlineData("At-delivery")]
    [InlineData("RTO-initiated")]
    [InlineData("RTO-delivered")]
    public void Classify_WhenProviderHasAnAgentOnTheOrder_ShouldNotBeSearching(string state)
    {
        ThirdPartyTaskStateClassifier.Classify(state)
            .Should().Be(ThirdPartyTaskProgress.AssignedOrBeyond,
                "a rider is on this order — cancelling and re-booking would strand them and double-bill us");
    }

    [Theory]
    [InlineData("Searching-for-agent")]
    [InlineData("searching_for_agent")]
    [InlineData("Pending")]
    [InlineData("UnFulfilled")]
    public void Classify_WhenProviderIsStillHunting_ShouldBeSearching(string state)
    {
        ThirdPartyTaskStateClassifier.Classify(state)
            .Should().Be(ThirdPartyTaskProgress.Searching);
    }

    [Theory]
    [InlineData("Order-cancelled")]
    [InlineData("Cancelled")]
    [InlineData("Failed")]
    public void Classify_WhenTaskIsDeadAtProvider_ShouldBeCancelledOrFailed(string state)
    {
        ThirdPartyTaskStateClassifier.Classify(state)
            .Should().Be(ThirdPartyTaskProgress.CancelledOrFailed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Some-New-State-They-Added")]
    public void Classify_WhenStateIsBlankOrUnrecognised_ShouldBeUnknownNotSearching(string? state)
    {
        // The dangerous default. An unknown state must never be read as "nobody is coming",
        // because that is what authorises a cancel.
        ThirdPartyTaskStateClassifier.Classify(state)
            .Should().Be(ThirdPartyTaskProgress.Unknown);
    }

    [Fact]
    public void Classify_ShouldNeverReportSearching_ForAnyStateMentioningAnAgentOrDelivery()
    {
        // Guard the whole family at once: if someone later adds a loose `Contains("pending")`
        // style rule that catches e.g. "Agent-assigned-pending", this fails.
        var agentStates = new[]
        {
            "Agent-assigned", "At-pickup", "Order-picked-up",
            "At-delivery", "Order-delivered", "RTO-initiated"
        };

        foreach (var state in agentStates)
        {
            ThirdPartyTaskStateClassifier.Classify(state)
                .Should().NotBe(ThirdPartyTaskProgress.Searching, "state '{0}' has a rider on it", state);
        }
    }
}
