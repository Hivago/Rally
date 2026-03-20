using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Cart.Abstractions;
using RallyAPI.Orders.Application.Cart.Commands.AddCartItem;
using Xunit;

namespace RallyAPI.Orders.Application.Tests.Cart;

public sealed class AddCartItemCommandHandlerTests
{
    private readonly ICartRepository _repository;
    private readonly ICartCacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddCartItemCommandHandler> _logger;
    private readonly AddCartItemCommandHandler _handler;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MenuItemId = Guid.NewGuid();

    public AddCartItemCommandHandlerTests()
    {
        _repository = Substitute.For<ICartRepository>();
        _cache = Substitute.For<ICartCacheService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _logger = Substitute.For<ILogger<AddCartItemCommandHandler>>();
        _handler = new AddCartItemCommandHandler(_repository, _cache, _unitOfWork, _logger);
    }

    [Fact]
    public async Task Handle_WhenNoExistingCart_ShouldCreateCartAndReturnSuccess()
    {
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns((Domain.Entities.Cart?)null);
        var command = BuildCommand(RestaurantId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.RestaurantId.Should().Be(RestaurantId);
        await _repository.Received(1).CreateAsync(Arg.Any<Domain.Entities.Cart>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Domain.Entities.Cart>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).SetAsync(CustomerId, Arg.Any<Domain.Entities.Cart>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExistingCartSameRestaurant_ShouldAddItemAndReturnSuccess()
    {
        var existingCart = Domain.Entities.Cart.Create(CustomerId, RestaurantId, "Dosa Corner");
        existingCart.AddItem(Guid.NewGuid(), "Filter Coffee", 40m, 1);
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(existingCart);
        var command = BuildCommand(RestaurantId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        await _repository.Received(1).UpdateAsync(existingCart, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CreateAsync(Arg.Any<Domain.Entities.Cart>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSameMenuItemAndOptions_ShouldMergeQuantities()
    {
        var existingCart = Domain.Entities.Cart.Create(CustomerId, RestaurantId, "Dosa Corner");
        existingCart.AddItem(MenuItemId, "Masala Dosa", 120m, 1);
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(existingCart);
        var command = BuildCommand(RestaurantId); // same MenuItemId, quantity 1

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1); // merged, not duplicated
        result.Value.Items.Single().Quantity.Should().Be(2); // 1 existing + 1 added
    }

    [Fact]
    public async Task Handle_WhenExistingCartDifferentRestaurant_AndReplaceCartFalse_ShouldReturnConflict()
    {
        var differentRestaurantId = Guid.NewGuid();
        var existingCart = Domain.Entities.Cart.Create(CustomerId, differentRestaurantId, "Burger House");
        existingCart.AddItem(Guid.NewGuid(), "Burger", 150m, 1);
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(existingCart);
        var command = BuildCommand(RestaurantId, replaceCart: false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Cart.RestaurantConflict");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExistingCartDifferentRestaurant_AndReplaceCartTrue_ShouldDeleteOldAndCreateNew()
    {
        var differentRestaurantId = Guid.NewGuid();
        var existingCart = Domain.Entities.Cart.Create(CustomerId, differentRestaurantId, "Burger House");
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(existingCart);
        var command = BuildCommand(RestaurantId, replaceCart: true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(RestaurantId);
        await _repository.Received(1).DeleteByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(CustomerId, Arg.Any<CancellationToken>());
        await _repository.Received(1).CreateAsync(Arg.Any<Domain.Entities.Cart>(), Arg.Any<CancellationToken>());
    }

    #region Helpers

    private static AddCartItemCommand BuildCommand(Guid restaurantId, bool replaceCart = false) =>
        new()
        {
            CustomerId = CustomerId,
            RestaurantId = restaurantId,
            RestaurantName = "Dosa Corner",
            MenuItemId = MenuItemId,
            Name = "Masala Dosa",
            UnitPrice = 120m,
            Quantity = 1,
            ReplaceCart = replaceCart,
        };

    #endregion
}
