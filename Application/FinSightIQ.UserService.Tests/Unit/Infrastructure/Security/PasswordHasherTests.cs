using FinSightIQ.UserService.Infrastructure.Security;
using FluentAssertions;

namespace FinSightIQ.UserService.Tests.Unit.Infrastructure.Security;

public sealed class PasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ShouldReturnValidBCryptHash()
    {
        // Arrange
        const string password = "TestPassphrase123!";

        // Act
        string hash = _hasher.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("$2"); // BCrypt signature
        hash.Length.Should().Be(60);   // Exactly 60 characters (FR-AUTH-03, DBD-001)
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        const string password = "TestPassphrase123!";
        string hash = _hasher.HashPassword(password);

        // Act
        bool isValid = _hasher.VerifyPassword(password, hash);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ShouldReturnFalse()
    {
        // Arrange
        const string password = "TestPassphrase123!";
        const string wrongPassword = "WrongPassword!";
        string hash = _hasher.HashPassword(password);

        // Act
        bool isValid = _hasher.VerifyPassword(wrongPassword, hash);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("$2a$12$invalidlength")]
    public void VerifyPassword_WithInvalidHashFormat_ShouldReturnFalseWithoutThrowing(string invalidHash)
    {
        // Act & Assert
        _hasher.Invoking(h => h.VerifyPassword("Password123!", invalidHash))
            .Should().NotThrow();

        _hasher.VerifyPassword("Password123!", invalidHash).Should().BeFalse();
    }
}
