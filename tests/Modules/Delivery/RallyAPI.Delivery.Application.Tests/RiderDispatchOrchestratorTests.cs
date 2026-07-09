using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using NSubstitute;
using RallyAPI.Delivery.Application.Services;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Delivery;
using RallyAPI.SharedKernel.Abstractions.Notifications;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Tests;

public class RiderDispatchOrchestratorTests
{
    private readonly IRiderQueryService _riderQueryService;
    private readonly IRiderNotificationService _notificationService;
    private readonly IThirdPartyDeliveryProvider _thirdPartyProvider;
    private readonly IDeliveryRequestRepository _repository;
    private readonly ILogger<RiderDispatchOrchestrator> _logger;

    // Legacy 3PL-first path (OwnFleetFirst = false). AcceptanceTimeoutSeconds = 0 skips Task.Delay.
    // The own-fleet-first broadcast path is covered by the OwnFleetFirst_* tests below, which
    // build their own orchestrator via BuildOwnFirstOrchestrator().
    private readonly DispatchOptions _options = new()
    {
        OwnFleetFirst = false,
        AcceptanceTimeoutSeconds = 0,
        SearchRadiusKm = 5,
        MaxRidersToTry = 5,
        RiderEarningsPercentage = 80
    };

    private readonly RiderDispatchOrchestrator _orchestrator;

    public RiderDispatchOrchestratorTests()
    {
        _riderQueryService = Substitute.For<IRiderQueryService>();
        _notificationService = Substitute.For<IRiderNotificationService>();
        _thirdPartyProvider = Substitute.For<IThirdPartyDeliveryProvider>();
        _repository = Substitute.For<IDeliveryRequestRepository>();
        _logger = Substitute.For<ILogger<RiderDispatchOrchestrator>>();

        _orchestrator = new RiderDispatchOrchestrator(
            _riderQueryService,
            _notificationService,
            _thirdPartyProvider,
            _repository,
            Options.Create(_options),
            _logger);

        _notificationService
            .SendDeliveryOfferAsync(Arg.Any<Guid>(), Arg.Any<DeliveryOfferNotification>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // ProRouting requires a follow-up update call to push our OTPs and move the task
        // from UnFulfilled -> Searching-for-Agent. The orchestrator NREs without this stub.
        _thirdPartyProvider
            .UpdateOrderAsync(Arg.Any<UpdateOrderRequest>(), Arg.Any<CancellationToken>())
            .Returns(UpdateOrderResult.Success("searching", "ProRouting"));

        // Terminal failure writes go through TryUpdateAsync; true = the Failed write
        // committed (no concurrent accept). Tests that simulate a lost concurrency race
        // override this to false.
        _repository
            .TryUpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Fact]
    public async Task DispatchAsync_When3PLAssigns_ShouldReturnSuccessWithThirdPartyFleet()
    {
        var deliveryRequest = BuildCreatedRequest();

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Success("TASK-001", deliveryRequest.OrderId.ToString(), "assigned", null, "ProRouting"));

        // After the 3PL wait the webhook has assigned the task. The orchestrator reads the
        // TRUE status via GetCurrentStatusAsync (a fresh DB read), not a stale tracking reload.
        _repository
            .GetCurrentStatusAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // Status is Searching3PL when we get here (set by orchestrator before 3PL call)
                if (deliveryRequest.Status == DeliveryRequestStatus.Searching3PL)
                    deliveryRequest.Assign3PLRider("TASK-001", "ProRouting", "Rahul", "+91999", null, 95m);
                return (DeliveryRequestStatus?)deliveryRequest.Status;
            });

        var result = await _orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.ThirdParty);
        result.ExternalTaskId.Should().Be("TASK-001");
    }

    [Fact]
    public async Task DispatchAsync_ThirdPartyFirst_WhenTaskCreated_HandsOffNonBlockingWithoutCancelling()
    {
        // Legacy 3PL-first path: the orchestrator no longer blocks 30s then cancels. It creates
        // the task, records the handoff (ThirdPartyDispatchedAt), and returns — the provider
        // webhook assigns an agent and the recovery service enforces the search timeout.
        var deliveryRequest = BuildCreatedRequest();

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Success("TASK-002", deliveryRequest.OrderId.ToString(), "searching", null, "ProRouting"));

        var result = await _orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.ThirdParty);
        result.ExternalTaskId.Should().Be("TASK-002");
        deliveryRequest.Status.Should().Be(DeliveryRequestStatus.Searching3PL);
        deliveryRequest.ThirdPartyDispatchedAt.Should().NotBeNull();
        // Non-blocking: no cancel, and own fleet was not touched.
        await _thirdPartyProvider.DidNotReceive().CancelTaskAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _riderQueryService.DidNotReceive().GetAvailableRidersAsync(
            Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_When3PLFails_ShouldFallBackToOwnFleetAndSucceed()
    {
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildCreatedRequest();

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Failure("Service unavailable", "ProRouting"));

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildRider(riderId) });

        _repository
            .GetByIdWithOffersAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        _repository
            .GetCurrentStatusAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // Rider accepts during the offer window.
                if (deliveryRequest.Status == DeliveryRequestStatus.SearchingOwnFleet)
                    deliveryRequest.AssignOwnFleetRider(riderId, "Suresh", "+919876543210");
                return (DeliveryRequestStatus?)deliveryRequest.Status;
            });

        var result = await _orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.OwnFleet);
        result.RiderId.Should().Be(riderId);
    }

    [Fact]
    public async Task DispatchAsync_When3PLFailsAndNoRidersAvailable_ShouldMarkFailedAndReturnFailure()
    {
        var deliveryRequest = BuildCreatedRequest();

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Failure("Service unavailable", "ProRouting"));

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AvailableRider>());

        var result = await _orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeFalse();
        deliveryRequest.Status.Should().Be(DeliveryRequestStatus.Failed);
    }

    [Fact]
    public async Task DispatchAsync_When3PLFailsAndAllRidersDecline_ShouldMarkFailedAfterExhaustingRiders()
    {
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildCreatedRequest();

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Failure("Service unavailable", "ProRouting"));

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildRider(riderId) });

        // Rider did not accept, status stays SearchingOwnFleet across both the reload
        // (offer expiry) and the fresh status probes.
        _repository
            .GetByIdWithOffersAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        _repository
            .GetCurrentStatusAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => (DeliveryRequestStatus?)deliveryRequest.Status);

        var result = await _orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeFalse();
        deliveryRequest.Status.Should().Be(DeliveryRequestStatus.Failed);
    }

    [Fact]
    public async Task DispatchAsync_WhenRiderAcceptsAsTerminalFailWriteRaces_ShouldHonorAssignmentNotFail()
    {
        // The residual sub-second race: every fresh status probe still reads SearchingOwnFleet,
        // but the rider's acceptance commits on another connection right before the terminal
        // Failed write. xmin catches it — TryUpdateAsync returns false (UPDATE matched 0 rows).
        // The orchestrator must treat that as the rider winning, NOT fail the delivery.
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildCreatedRequest();

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Failure("Service unavailable", "ProRouting"));

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildRider(riderId) });

        _repository
            .GetByIdWithOffersAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        // Probes never see the accept (stale identity-map symptom the token defends against).
        _repository
            .GetCurrentStatusAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => (DeliveryRequestStatus?)deliveryRequest.Status);

        // The guarded terminal write loses the race.
        _repository
            .TryUpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.OwnFleet);
    }

    [Fact]
    public async Task DispatchAsync_When3PLFails_ShouldNotCancelAnyTask()
    {
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildCreatedRequest();

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Failure("Service unavailable", "ProRouting"));

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AvailableRider>());

        await _orchestrator.DispatchAsync(deliveryRequest);

        await _thirdPartyProvider.DidNotReceive().CancelTaskAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------------------
    // Own-fleet-first broadcast path (OwnFleetFirst = true)
    // ----------------------------------------------------------------------------

    private RiderDispatchOrchestrator BuildOwnFirstOrchestrator(int acceptanceTimeoutSeconds = 0) =>
        new(
            _riderQueryService,
            _notificationService,
            _thirdPartyProvider,
            _repository,
            Options.Create(new DispatchOptions
            {
                OwnFleetFirst = true,
                AcceptanceTimeoutSeconds = acceptanceTimeoutSeconds,
                OfferPollIntervalSeconds = 1,
                SearchRadiusKm = 5,
                MaxRidersToTry = 5,
                RiderEarningsPercentage = 80
            }),
            _logger);

    [Fact]
    public async Task OwnFleetFirst_ShouldBroadcastToAllEligibleRiders_AndAssignWithout3PL()
    {
        var riderA = Guid.NewGuid();
        var riderB = Guid.NewGuid();
        var deliveryRequest = BuildCreatedRequest();

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildRider(riderA), BuildRider(riderB) });

        // Window (0s) elapses; the fresh status read reflects that riderA accepted mid-broadcast.
        _repository
            .GetCurrentStatusAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (deliveryRequest.Status == DeliveryRequestStatus.SearchingOwnFleet)
                    deliveryRequest.AssignOwnFleetRider(riderA, "Suresh", "+919876543210");
                return (DeliveryRequestStatus?)deliveryRequest.Status;
            });
        _repository
            .GetByIdFreshAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        var orchestrator = BuildOwnFirstOrchestrator();

        var result = await orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.OwnFleet);
        // Broadcast: an offer notification went to BOTH eligible riders.
        await _notificationService.Received(2).SendDeliveryOfferAsync(
            Arg.Any<Guid>(), Arg.Any<DeliveryOfferNotification>(), Arg.Any<CancellationToken>());
        // 3PL was never touched — own fleet took it first.
        await _thirdPartyProvider.DidNotReceive().CreateTaskAsync(
            Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OwnFleetFirst_WhenRiderAcceptsDuringPollWindow_ShouldAssignOwnFleet()
    {
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildCreatedRequest();

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildRider(riderId) });

        // The poll reads a fresh accept committed on another connection.
        _repository
            .GetCurrentStatusAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (deliveryRequest.Status == DeliveryRequestStatus.SearchingOwnFleet)
                    deliveryRequest.AssignOwnFleetRider(riderId, "Suresh", "+919876543210");
                return (DeliveryRequestStatus?)deliveryRequest.Status;
            });
        _repository
            .GetByIdFreshAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        // 1s window so the poll loop actually runs one iteration.
        var orchestrator = BuildOwnFirstOrchestrator(acceptanceTimeoutSeconds: 1);

        var result = await orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.OwnFleet);
        result.RiderId.Should().Be(riderId);
        await _thirdPartyProvider.DidNotReceive().CreateTaskAsync(
            Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OwnFleetFirst_WhenNoOwnRiders_ShouldFallBackTo3PLImmediately()
    {
        var deliveryRequest = BuildCreatedRequest();

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AvailableRider>());

        _repository
            .GetByIdFreshAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Success("TASK-9", deliveryRequest.OrderId.ToString(), "searching", null, "ProRouting"));

        _repository
            .GetCurrentStatusAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (deliveryRequest.Status == DeliveryRequestStatus.Searching3PL)
                    deliveryRequest.Assign3PLRider("TASK-9", "ProRouting", "Rahul", "+91999", null, 95m);
                return (DeliveryRequestStatus?)deliveryRequest.Status;
            });

        var orchestrator = BuildOwnFirstOrchestrator();

        var result = await orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.ThirdParty);
        // No riders → no offers broadcast.
        await _notificationService.DidNotReceive().SendDeliveryOfferAsync(
            Arg.Any<Guid>(), Arg.Any<DeliveryOfferNotification>(), Arg.Any<CancellationToken>());
        await _thirdPartyProvider.Received(1).CreateTaskAsync(
            Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OwnFleetFirst_WhenNoOwnRiderAccepts_ShouldFallBackTo3PLAndSucceed()
    {
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildCreatedRequest();

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildRider(riderId) });

        // Nobody accepts: reload after the window still shows SearchingOwnFleet.
        _repository
            .GetByIdFreshAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Success("TASK-7", deliveryRequest.OrderId.ToString(), "searching", null, "ProRouting"));

        _repository
            .GetCurrentStatusAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (deliveryRequest.Status == DeliveryRequestStatus.Searching3PL)
                    deliveryRequest.Assign3PLRider("TASK-7", "ProRouting", "Rahul", "+91999", null, 95m);
                return (DeliveryRequestStatus?)deliveryRequest.Status;
            });

        var orchestrator = BuildOwnFirstOrchestrator();

        var result = await orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.ThirdParty);
        result.ExternalTaskId.Should().Be("TASK-7");
    }

    [Fact]
    public async Task OwnFleetFirst_WhenOwnFleetAnd3PLBothFail_ShouldMarkFailed()
    {
        var deliveryRequest = BuildCreatedRequest();

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AvailableRider>());

        _repository
            .GetByIdFreshAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Failure("Service unavailable", "ProRouting"));

        var orchestrator = BuildOwnFirstOrchestrator();

        var result = await orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeFalse();
        deliveryRequest.Status.Should().Be(DeliveryRequestStatus.Failed);
        deliveryRequest.FailureReason.Should().Be(DeliveryFailureReason.NoRidersAvailable);
    }

    [Fact]
    public async Task OwnFleetFirst_WhenRiderRejects_ShouldFallBackTo3PL_NotSucceedAsOwnFleetOrCrash()
    {
        // Regression: a rider REJECTING an offer bumps the delivery's xmin from another
        // connection. The dispatcher's own-fleet writes (expire offers / move to 3PL) then lose
        // the concurrency race while status is still SearchingOwnFleet. The orchestrator must
        // re-read the real status and fall back to 3PL — NOT misread the token change as an
        // acceptance (return OwnFleet) and NOT throw a concurrency exception.
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildCreatedRequest();

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildRider(riderId) });

        _repository
            .GetByIdFreshAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        // No rider accepts (the one rider rejected) — every fresh status probe still reads
        // SearchingOwnFleet until we successfully transition to 3PL.
        _repository
            .GetCurrentStatusAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (deliveryRequest.Status == DeliveryRequestStatus.Searching3PL)
                    deliveryRequest.Assign3PLRider("TASK-R", "ProRouting", "Rahul", "+91999", null, 95m);
                return (DeliveryRequestStatus?)deliveryRequest.Status;
            });

        // The decline bumped xmin: any write attempted WHILE still SearchingOwnFleet loses the
        // race (returns false). Writes made after transitioning off SearchingOwnFleet commit.
        _repository
            .TryUpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((DeliveryRequest)ci[0]!).Status != DeliveryRequestStatus.SearchingOwnFleet);

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Success("TASK-R", deliveryRequest.OrderId.ToString(), "searching", null, "ProRouting"));

        var orchestrator = BuildOwnFirstOrchestrator();

        var result = await orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.ThirdParty);
        deliveryRequest.Status.Should().NotBe(DeliveryRequestStatus.Failed);
    }

    [Fact]
    public async Task OwnFleetFirst_WhenThirdPartyAlreadyDispatched_RetryOwnFleetFailsInsteadOfLoopingBackTo3PL()
    {
        // Simulate the post-3PL-timeout state the recovery service produces: 3PL was tried
        // (ThirdPartyDispatchedAt set) then handed back to own fleet. If this own-fleet retry
        // also finds no rider, the delivery must be Failed — NOT dispatched to 3PL a second time.
        var deliveryRequest = BuildCreatedRequest();
        deliveryRequest.StartSearchingOwnFleet();
        deliveryRequest.StartSearching3PL();
        deliveryRequest.MarkThirdPartyDispatched("TASK-OLD");
        deliveryRequest.TransitionToOwnFleetSearch(); // back to own fleet, keeps ThirdPartyDispatchedAt
        deliveryRequest.ThirdPartyDispatchedAt.Should().NotBeNull();

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AvailableRider>());

        _repository
            .GetByIdFreshAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        var orchestrator = BuildOwnFirstOrchestrator();

        var result = await orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeFalse();
        deliveryRequest.Status.Should().Be(DeliveryRequestStatus.Failed);
        // Must NOT book 3PL again.
        await _thirdPartyProvider.DidNotReceive().CreateTaskAsync(
            Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>());
    }

    #region Helpers

    private static DeliveryRequest BuildCreatedRequest()
    {
        return DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            orderNumber: "ORD-TEST-001",
            quoteId: null,
            quotedPrice: 100m,
            pickupLat: 12.935,
            pickupLng: 77.624,
            pickupPincode: "560095",
            pickupAddress: "Restaurant Road",
            pickupContactName: "Biryani House",
            pickupContactPhone: "+919876543210",
            dropLat: 12.971,
            dropLng: 77.594,
            dropPincode: "560025",
            dropAddress: "Customer Street",
            dropContactName: "Priya Singh",
            dropContactPhone: "+919845678901");
    }

    private static AvailableRider BuildRider(Guid riderId) => new()
    {
        RiderId = riderId,
        Name = "Suresh Kumar",
        Phone = "+919876543210",
        Latitude = 12.94,
        Longitude = 77.62,
        DistanceToPickupKm = 1.2,
        VehicleType = "Bike",
        LocationUpdatedAt = DateTime.UtcNow
    };

    #endregion
}
