using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Cart.Abstractions;
using RallyAPI.Orders.Application.Cart.Commands.SyncCart;
using Xunit;

namespace RallyAPI.Orders.Application.Tests.Cart;

public sealed class SyncCartCommandHandlerTests
{
    private readonly ICartRepository _repository;
    private readonly ICartCacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SyncCartCommandHandler> _logger;
    private readonly SyncCartCommandHandler _handler;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public SyncCartCommandHandlerTests()
    {
        _repository = Substitute.For<ICartRepository>();
        _cache = Substitute.For<ICartCacheService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _logger = Substitute.For<ILogger<SyncCartCommandHandler>>();
        _handler = new SyncCartCommandHandler(_repository, _cache, _unitOfWork, _logger);
    }

    [Fact]
    public async Task Handle_WhenNoItems_AndNoServerCart_ShouldReturnFailure()
    {
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns((Domain.Entities.Cart?)null);
        var command = BuildCommand([]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Cart.Empty");
    }

    [Fact]
    public async Task Handle_WhenNoItems_AndServerCartExists_ShouldReturnServerCartWithoutSaving()
    {
        var serverCart = Domain.Entities.Cart.Create(CustomerId, RestaurantId, "Dosa Corner");
        serverCart.AddItem(Guid.NewGuid(), "Masala Dosa", 120m, 1);
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(serverCart);
        var command = BuildCommand([]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoServerCart_ShouldCreateCartFromGuestItems()
    {
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns((Domain.Entities.Cart?)null);
        var guestItems = new List<SyncCartItemDto>
        {
            new(Guid.NewGuid(), "Masala Dosa", 120m, 2, null, null),
            new(Guid.NewGuid(), "Filter Coffee", 40m, 1, null, null),
        };
        var command = BuildCommand(guestItems);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.ItemCount.Should().Be(3); // 2 + 1
        result.Value.RestaurantId.Should().Be(RestaurantId);
        await _repository.Received(1).CreateAsync(Arg.Any<Domain.Entities.Cart>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSameRestaurant_ShouldMergeMatchingItemsIntoServerCart()
    {
        var sharedMenuItemId = Guid.NewGuid();
        var serverCart = Domain.Entities.Cart.Create(CustomerId, RestaurantId, "Dosa Corner");
        serverCart.AddItem(sharedMenuItemId, "Masala Dosa", 120m, 1);
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(serverCart);
        var guestItems = new List<SyncCartItemDto>
        {
            new(sharedMenuItemId, "Masala Dosa", 120m, 2, null, null), // same item → should merge
        };
        var command = BuildCommand(guestItems);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1); // merged, not duplicated
        result.Value.ItemCount.Should().Be(3); // 1 existing + 2 from guest
        await _repository.Received(1).UpdateAsync(serverCart, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CreateAsync(Arg.Any<Domain.Entities.Cart>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDifferentRestaurant_AndReplaceCartFalse_ShouldReturnConflict()
    {
        var differentRestaurantId = Guid.NewGuid();
        var serverCart = Domain.Entities.Cart.Create(CustomerId, differentRestaurantId, "Burger House");
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(serverCart);
        var guestItems = new List<SyncCartItemDto>
        {
            new(Guid.NewGuid(), "Masala Dosa", 120m, 1, null, null),
        };
        var command = BuildCommand(guestItems, replaceCart: false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Cart.RestaurantConflict");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDifferentRestaurant_AndReplaceCartTrue_ShouldDeleteOldAndCreateFromGuestItems()
    {
        var differentRestaurantId = Guid.NewGuid();
        var serverCart = Domain.Entities.Cart.Create(CustomerId, differentRestaurantId, "Burger House");
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(serverCart);
        var guestItems = new List<SyncCartItemDto>
        {
            new(Guid.NewGuid(), "Masala Dosa", 120m, 1, null, null),
        };
        var command = BuildCommand(guestItems, replaceCart: true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(RestaurantId);
        result.Value.Items.Should().HaveCount(1);
        await _repository.Received(1).DeleteByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(CustomerId, Arg.Any<CancellationToken>());
        await _repository.Received(1).CreateAsync(Arg.Any<Domain.Entities.Cart>(), Arg.Any<CancellationToken>());
    }

    #region Helpers

    private SyncCartCommand BuildCommand(List<SyncCartItemDto> items, bool replaceCart = false) =>
        new()
        {
            CustomerId = CustomerId,
            RestaurantId = RestaurantId,
            RestaurantName = "Dosa Corner",
            Items = items,
            ReplaceCart = replaceCart,
        };

    #endregion
}
