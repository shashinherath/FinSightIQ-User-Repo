using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Infrastructure.Repositories;
using FluentAssertions;

namespace FinSightIQ.UserService.Tests.Integration;

[Collection("DatabaseCollection")]
public sealed class WatchlistRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly UserRepository _userRepository;
    private readonly WatchlistRepository _watchlistRepository;

    public WatchlistRepositoryTests(DatabaseFixture fixture)
    {
        _userRepository = new UserRepository(fixture.Dapper);
        _watchlistRepository = new WatchlistRepository(fixture.Dapper);
    }

    [Fact]
    public async Task AddOrGetAsync_WhenCalledTwiceWithSameSymbol_ShouldBeIdempotent()
    {
        // Arrange (TC-AUTH-14)
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), $"wl_{Guid.NewGuid():N}@example.com", "hash", UserRole.Analyst, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await _userRepository.CreateAsync(user);

        // Act: add AAPL first time
        WatchlistItemDto item1 = await _watchlistRepository.AddOrGetAsync(user.Id, "AAPL");

        // Act: add AAPL second time
        WatchlistItemDto item2 = await _watchlistRepository.AddOrGetAsync(user.Id, "AAPL");

        // Assert
        item1.Should().NotBeNull();
        item2.Should().NotBeNull();
        item1.Id.Should().Be(item2.Id); // Exactly one record exists

        IReadOnlyList<WatchlistItemDto> items = await _watchlistRepository.GetByUserIdAsync(user.Id);
        items.Should().HaveCount(1);
        items[0].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteExistingWatchlistEntry()
    {
        // Arrange
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), $"wl_del_{Guid.NewGuid():N}@example.com", "hash", UserRole.Analyst, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await _userRepository.CreateAsync(user);

        await _watchlistRepository.AddOrGetAsync(user.Id, "MSFT");

        // Act
        bool removed = await _watchlistRepository.RemoveAsync(user.Id, "MSFT");

        // Assert
        removed.Should().BeTrue();
        IReadOnlyList<WatchlistItemDto> items = await _watchlistRepository.GetByUserIdAsync(user.Id);
        items.Should().BeEmpty();
    }
}
