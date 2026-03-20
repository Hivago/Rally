using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Cart.Abstractions;
using RallyAPI.Orders.Application.Cart.Commands.UpdateCartItem;
using Xunit;

namespace RallyAPI.Orders.Application.Tests.Cart;

public sealed class UpdateCartItemCommandHandlerTests
{
    private readonly ICartRepository _repository;
    private readonly ICartCacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateCartItemCommandHandler> _logger;
    private readonly UpdateCartItemCommandHandler _handler;

    private static readonly Guid CustomerId = Guid.NewGuid();

    public UpdateCartItemCommandHandlerTests()
    {
        _repository = Substitute.For<ICartRepository>();
        _cache = Substitute.For<ICartCacheService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _logger = Substitute.For<ILogger<UpdateCartItemCommandHandler>>();
        _handler = new UpdateCartItemCommandHandler(_repository, _cache, _unitOfWork, _logger);
    }

    [Fact]
    public async Task Handle_WhenCartNotFound_ShouldReturnFailure()
    {
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns((Domain.Entities.Cart?)null);
        var command = new UpdateCartItemCommand { CustomerId = CustomerId, ItemId = Guid.NewGuid(), Quantity = 3 };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Cart.NotFound");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenItemNotFoundInCart_ShouldReturnFailure()
    {
        var cart = Domain.Entities.Cart.Create(CustomerId, Guid.NewGuid(), "Dosa Corner");
        cart.AddItem(Guid.NewGuid(), "Masala Dosa", 120m, 1);
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        var nonExistentItemId = Guid.NewGuid();
        var command = new UpdateCartItemCommand { CustomerId = CustomerId, ItemId = nonExistentItemId, Quantity = 3 };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Cart.ItemNotFound");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenItemExists_ShouldUpdateQuantityAndReturnSuccess()
    {
        var cart = Domain.Entities.Cart.Create(CustomerId, Guid.NewGuid(), "Dosa Corner");
        var item = cart.AddItem(Guid.NewGuid(), "Masala Dosa", 120m, 1);
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        var command = new UpdateCartItemCommand { CustomerId = CustomerId, ItemId = item.Id, Quantity = 5 };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Single().Quantity.Should().Be(5);
        await _repository.Received(1).UpdateAsync(cart, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).SetAsync(CustomerId, cart, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenItemExists_ShouldUpdateSubTotalAccordingly()
    {
        var cart = Domain.Entities.Cart.Create(CustomerId, Guid.NewGuid(), "Dosa Corner");
        var item = cart.AddItem(Guid.NewGuid(), "Masala Dosa", 120m, 1); // 1 × 120 = 120
        _repository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        var command = new UpdateCartItemCommand { CustomerId = CustomerId, ItemId = item.Id, Quantity = 3 };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SubTotal.Should().Be(360m); // 3 × 120
    }
}
