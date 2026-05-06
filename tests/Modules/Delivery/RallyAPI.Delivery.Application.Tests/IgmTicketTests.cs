using FluentAssertions;
using Xunit;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Application.Tests;

public class IgmTicketTests
{
    private static IgmTicket NewTicket() =>
        IgmTicket.Create(
            id: Guid.NewGuid(),
            deliveryRequestId: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            orderNumber: "TEST-0001",
            issueType: IgmIssueType.DelayInDelivery,
            category: "FULFILLMENT",
            subCategory: "FLM08",
            descriptionShort: "Delivery delayed by 1 hour",
            descriptionLong: null,
            raisedByAdminId: Guid.NewGuid());

    [Fact]
    public void Create_NewTicket_StateIsOpen()
    {
        var ticket = NewTicket();

        ticket.State.Should().Be(IgmTicketState.Open);
        ticket.ExternalIssueId.Should().BeNull();
        ticket.ResolutionAction.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptyCategory_Throws(string category)
    {
        var act = () => IgmTicket.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "X",
            IgmIssueType.DelayInDelivery, category, "FLM02", "desc", null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkPushed_FromOpen_TransitionsToProcessing()
    {
        var ticket = NewTicket();

        ticket.MarkPushed("issmp2pre_xyz_123");

        ticket.State.Should().Be(IgmTicketState.Processing);
        ticket.ExternalIssueId.Should().Be("issmp2pre_xyz_123");
        ticket.PushedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkPushed_TwiceFromOpen_SecondCallThrows()
    {
        var ticket = NewTicket();
        ticket.MarkPushed("issmp2pre_xyz_123");

        var act = () => ticket.MarkPushed("issmp2pre_xyz_456");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Only Open tickets can be pushed*");
    }

    [Fact]
    public void MarkResolved_FromProcessing_RecordsResolutionAndState()
    {
        var ticket = NewTicket();
        ticket.MarkPushed("issmp2pre_xyz_123");

        ticket.MarkResolved(
            IgmResolutionAction.Refund,
            "Refunding full retail amount.",
            "NA. Refund amount: 130.0",
            130m);

        ticket.State.Should().Be(IgmTicketState.Resolved);
        ticket.ResolutionAction.Should().Be(IgmResolutionAction.Refund);
        ticket.RefundAmount.Should().Be(130m);
        ticket.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkResolved_FromOpen_Throws()
    {
        var ticket = NewTicket();

        var act = () => ticket.MarkResolved(IgmResolutionAction.NoAction, null, null, null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Close_FromResolved_TransitionsToClosed()
    {
        var ticket = NewTicket();
        ticket.MarkPushed("issmp2pre_xyz_123");
        ticket.MarkResolved(IgmResolutionAction.Refund, "ok", null, 50m);

        ticket.Close(rating: "THUMBS-UP", refundByLsp: true, refundToClient: true);

        ticket.State.Should().Be(IgmTicketState.Closed);
        ticket.Rating.Should().Be("THUMBS-UP");
        ticket.RefundByLsp.Should().BeTrue();
        ticket.RefundToClient.Should().BeTrue();
        ticket.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public void Close_FromProcessing_Throws()
    {
        var ticket = NewTicket();
        ticket.MarkPushed("issmp2pre_xyz_123");

        var act = () => ticket.Close("THUMBS-DOWN", false, false);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Only Resolved tickets can be closed*");
    }
}
