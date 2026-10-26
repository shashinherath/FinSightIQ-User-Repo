using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Infrastructure.Repositories;
using FluentAssertions;

namespace FinSightIQ.UserService.Tests.Integration;

[Collection("DatabaseCollection")]
public sealed class RefreshTokenRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly UserRepository _userRepository;
    private readonly RefreshTokenRepository _tokenRepository;

    public RefreshTokenRepositoryTests(DatabaseFixture fixture)
    {
        _userRepository = new UserRepository(fixture.Dapper);
        _tokenRepository = new RefreshTokenRepository(fixture.Dapper);
    }

    [Fact]
    public async Task RotateTokenAsync_ShouldMarkOldTokenUsedAndInsertNewToken()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), $"rot_{Guid.NewGuid():N}@example.com", "hash", UserRole.Analyst, false, now, now);
        await _userRepository.CreateAsync(user);

        Guid familyId = Guid.NewGuid();
        string oldHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        RefreshTokenRecordDto oldToken = new RefreshTokenRecordDto(Guid.NewGuid(), user.Id, oldHash, familyId, DateTimeOffset.UtcNow.AddDays(7));
        await _tokenRepository.CreateAsync(oldToken);

        string newHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        RefreshTokenRecordDto newToken = new RefreshTokenRecordDto(Guid.NewGuid(), user.Id, newHash, familyId, DateTimeOffset.UtcNow.AddDays(7));

        // Act: Rotate old token into new token
        await _tokenRepository.RotateTokenAsync(oldToken.Id, newToken);

        // Assert
        RefreshTokenRecordDto? oldRetrieved = await _tokenRepository.GetByHashAsync(oldHash);
        oldRetrieved.Should().NotBeNull();
        oldRetrieved!.IsUsed.Should().BeTrue();
        oldRetrieved.UsedAt.Should().NotBeNull();

        RefreshTokenRecordDto? newRetrieved = await _tokenRepository.GetByHashAsync(newHash);
        newRetrieved.Should().NotBeNull();
        newRetrieved!.IsUsed.Should().BeFalse();
        newRetrieved.FamilyId.Should().Be(familyId);
    }

    [Fact]
    public async Task RevokeFamilyAsync_ShouldRevokeAllTokensInSameFamily()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), $"family_{Guid.NewGuid():N}@example.com", "hash", UserRole.Analyst, false, now, now);
        await _userRepository.CreateAsync(user);

        Guid familyId = Guid.NewGuid();
        string hash1 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        string hash2 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        RefreshTokenRecordDto token1 = new RefreshTokenRecordDto(Guid.NewGuid(), user.Id, hash1, familyId, DateTimeOffset.UtcNow.AddDays(7));
        RefreshTokenRecordDto token2 = new RefreshTokenRecordDto(Guid.NewGuid(), user.Id, hash2, familyId, DateTimeOffset.UtcNow.AddDays(7));

        await _tokenRepository.CreateAsync(token1);
        await _tokenRepository.CreateAsync(token2);

        // Act: Revoke the entire family (stolen token replay mitigation)
        await _tokenRepository.RevokeFamilyAsync(familyId);

        // Assert
        RefreshTokenRecordDto? t1 = await _tokenRepository.GetByHashAsync(hash1);
        RefreshTokenRecordDto? t2 = await _tokenRepository.GetByHashAsync(hash2);

        t1!.IsRevoked.Should().BeTrue();
        t2!.IsRevoked.Should().BeTrue();
    }
}
