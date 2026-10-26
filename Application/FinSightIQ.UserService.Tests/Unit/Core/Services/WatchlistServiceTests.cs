using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using FinSightIQ.UserService.Core.Services;
using FinSightIQ.UserService.Core.Validators;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinSightIQ.UserService.Tests.Unit.Core.Services;

public sealed class WatchlistServiceTests
{
    private readonly IWatchlistRepository _repository = Substitute.For<IWatchlistRepository>();
    private readonly WatchlistService _sut;

    public WatchlistServiceTests()
    {
        _sut = new WatchlistService(
            _repository,
            new WatchlistAddRequestValidator(),
            NullLogger<WatchlistService>.Instance);
    }

    [Fact]
    public async Task AddSymbolAsync_ShouldNormalizeToUppercaseAndPersist()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        const string symbol = "aapl";
        WatchlistItemDto expectedItem = new WatchlistItemDto(Guid.NewGuid(), userId, "AAPL", DateTimeOffset.UtcNow);

        _repository.AddOrGetAsync(userId, "AAPL", Arg.Any<CancellationToken>()).Returns(expectedItem);

        // Act
        Result<WatchlistItemDto> result = await _sut.AddSymbolAsync(userId, symbol);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Symbol.Should().Be("AAPL");
        await _repository.Received(1).AddOrGetAsync(userId, "AAPL", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddSymbolAsync_WithInvalidSymbol_ShouldReturnValidationError()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        const string invalidSymbol = "INVALID$SYMBOL";

        // Act
        Result<WatchlistItemDto> result = await _sut.AddSymbolAsync(userId, invalidSymbol);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_FAILED");
        await _repository.DidNotReceiveWithAnyArgs().AddOrGetAsync(default, default!, default);
    }

    [Fact]
    public async Task RemoveSymbolAsync_ShouldNormalizeToUppercaseAndCallRepository()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        const string symbol = "msft";
        _repository.RemoveAsync(userId, "MSFT", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        Result result = await _sut.RemoveSymbolAsync(userId, symbol);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).RemoveAsync(userId, "MSFT", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveSymbolAsync_WhenSymbolIsEmpty_ShouldReturnValidationError()
    {
        // Arrange
        Guid userId = Guid.NewGuid();

        // Act
        Result result = await _sut.RemoveSymbolAsync(userId, "   ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_FAILED");
    }
}
