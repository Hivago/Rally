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

    // AcceptanceTimeoutSeconds = 0 skips Task.Delay
    private readonly DispatchOptions _options = new()
    {
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
    }

    [Fact]
    public async Task DispatchAsync_When3PLAssigns_ShouldReturnSuccessWithThirdPartyFleet()
    {
        var deliveryRequest = BuildCreatedRequest();

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Success("TASK-001", deliveryRequest.OrderId.ToString(), "assigned", null, "ProRouting"));

        // Reload after timeout: webhook has fired, status = Assigned3PL
        _repository
            .GetByIdWithOffersAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // Status is Searching3PL when we get here (set by orchestrator before 3PL call)
                deliveryRequest.Assign3PLRider("TASK-001", "ProRouting", "Rahul", "+91999", null, 95m);
                return deliveryRequest;
            });

        var result = await _orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.ThirdParty);
        result.ExternalTaskId.Should().Be("TASK-001");
    }

    [Fact]
    public async Task DispatchAsync_When3PLTimesOut_ShouldCancelTaskAndFallBackToOwnFleet()
    {
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildCreatedRequest();
        var callCount = 0;

        _thirdPartyProvider
            .CreateTaskAsync(Arg.Any<CreateTaskRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTaskResult.Success("TASK-002", deliveryRequest.OrderId.ToString(), "searching", null, "ProRouting"));

        _riderQueryService
            .GetAvailableRidersAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildRider(riderId) });

        _repository
            .GetByIdWithOffersAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First reload: still Searching3PL → orchestrator detects timeout
                    return deliveryRequest;
                }
                // Second reload (own fleet): rider accepted
                deliveryRequest.AssignOwnFleetRider(riderId, "Suresh", "+919876543210");
                return deliveryRequest;
            });

        var result = await _orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeTrue();
        result.FleetType.Should().Be(FleetType.OwnFleet);
        await _thirdPartyProvider.Received(1).CancelTaskAsync("TASK-002", Arg.Any<string>(), Arg.Any<CancellationToken>());
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
            .Returns(_ =>
            {
                deliveryRequest.AssignOwnFleetRider(riderId, "Suresh", "+919876543210");
                return deliveryRequest;
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

        // Reload: rider did not accept, status stays SearchingOwnFleet
        _repository
            .GetByIdWithOffersAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(_ => deliveryRequest);

        var result = await _orchestrator.DispatchAsync(deliveryRequest);

        result.IsSuccess.Should().BeFalse();
        deliveryRequest.Status.Should().Be(DeliveryRequestStatus.Failed);
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
