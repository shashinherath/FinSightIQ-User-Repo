using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Infrastructure.Repositories;
using FluentAssertions;

namespace FinSightIQ.UserService.Tests.Integration;

[Collection("DatabaseCollection")]
public sealed class UserRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly UserRepository _repository;

    public UserRepositoryTests(DatabaseFixture fixture)
    {
        _repository = new UserRepository(fixture.Dapper);
    }

    [Fact]
    public async Task CreateAsync_AndGetById_ShouldPersistAndRehydrateUser()
    {
        // Arrange
        string email = $"test_{Guid.NewGuid():N}@example.com";
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), email, "$2a$12$mockhashmockhashmockhashmockhashmockhash60chars", UserRole.Analyst, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        // Act
        UserRecordDto created = await _repository.CreateAsync(user);
        UserRecordDto? retrieved = await _repository.GetByIdAsync(created.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(created.Id);
        retrieved.Email.Should().Be(email);
        retrieved.Role.Should().Be(UserRole.Analyst);
        retrieved.IsSuspended.Should().BeFalse();
        retrieved.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldMatchCaseInsensitively()
    {
        // Arrange
        string uniqueId = Guid.NewGuid().ToString("N");
        string email = $"user_{uniqueId}@example.com";
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), email, "hash", UserRole.Analyst, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await _repository.CreateAsync(user);

        // Act: query with uppercase
        UserRecordDto? retrieved = await _repository.GetByEmailAsync($"USER_{uniqueId}@EXAMPLE.COM");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task DuplicateEmail_ShouldViolateUniqueConstraint()
    {
        // Arrange
        string email = $"duplicate_{Guid.NewGuid():N}@example.com";
        UserRecordDto user1 = new UserRecordDto(Guid.NewGuid(), email, "hash1", UserRole.Analyst, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        UserRecordDto user2 = new UserRecordDto(Guid.NewGuid(), email.ToUpperInvariant(), "hash2", UserRole.Viewer, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        await _repository.CreateAsync(user1);

        // Act & Assert
        Func<Task> act = async () => await _repository.CreateAsync(user2);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SoftDelete_ShouldExcludeUserFromGetByEmail()
    {
        // Arrange
        string email = $"softdelete_{Guid.NewGuid():N}@example.com";
        UserRecordDto user = new UserRecordDto(Guid.NewGuid(), email, "hash", UserRole.Analyst, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        UserRecordDto created = await _repository.CreateAsync(user);

        // Act
        UserRecordDto softDeleted = created with { DeletedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        await _repository.UpdateAsync(softDeleted);

        // Assert
        UserRecordDto? retrieved = await _repository.GetByEmailAsync(email);
        retrieved.Should().BeNull();
    }
}
