using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Cart.Abstractions;
using RallyAPI.Orders.Application.Cart.Commands.ClearCart;
using Xunit;

namespace RallyAPI.Orders.Application.Tests.Cart;

public sealed class ClearCartCommandHandlerTests
{
    private readonly ICartRepository _repository;
    private readonly ICartCacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ClearCartCommandHandler> _logger;
    private readonly ClearCartCommandHandler _handler;

    public ClearCartCommandHandlerTests()
    {
        _repository = Substitute.For<ICartRepository>();
        _cache = Substitute.For<ICartCacheService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _logger = Substitute.For<ILogger<ClearCartCommandHandler>>();
        _handler = new ClearCartCommandHandler(_repository, _cache, _unitOfWork, _logger);
    }

    [Fact]
    public async Task Handle_ShouldDeleteCartFlushCacheAndReturnSuccess()
    {
        var customerId = Guid.NewGuid();
        var command = new ClearCartCommand(customerId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).DeleteByCustomerIdAsync(customerId, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(customerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAlwaysSucceedEvenIfCartDoesNotExist()
    {
        // DeleteByCustomerIdAsync is a no-op if cart doesn't exist (EF Core ExecuteDeleteAsync)
        var customerId = Guid.NewGuid();
        var command = new ClearCartCommand(customerId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
