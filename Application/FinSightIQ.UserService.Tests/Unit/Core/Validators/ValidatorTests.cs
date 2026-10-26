using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Validators;
using FluentAssertions;
using FluentValidation.Results;

namespace FinSightIQ.UserService.Tests.Unit.Core.Validators;

public sealed class ValidatorTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly WatchlistAddRequestValidator _watchlistValidator = new();

    [Fact]
    public async Task RegisterValidator_WithValidDetails_ShouldPass()
    {
        // Arrange
        RegisterRequest request = new RegisterRequest("analyst@example.com", "Str0ngPassphrase!");

        // Act
        ValidationResult result = await _registerValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("short1A", "Must be at least 12 characters.")]                       // Less than 12 chars
    [InlineData("alllowercase123!", "Must contain at least one uppercase letter.")]   // No uppercase
    [InlineData("ALLUPPERCASE123!", "Must contain at least one lowercase letter.")]   // No lowercase
    [InlineData("NoDigitsHereAtAll!", "Must contain at least one digit.")]            // No digit
    public async Task RegisterValidator_WithWeakPassword_ShouldFailWithExpectedMessage(
        string weakPassword,
        string expectedError)
    {
        // Arrange
        RegisterRequest request = new RegisterRequest("user@example.com", weakPassword);

        // Act
        ValidationResult result = await _registerValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == expectedError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("notanemail")]
    [InlineData("@missinguser.com")]
    [InlineData("user@")]
    public async Task RegisterValidator_WithInvalidEmail_ShouldFail(string invalidEmail)
    {
        // Arrange
        RegisterRequest request = new RegisterRequest(invalidEmail, "ValidPassphrase123!");

        // Act
        ValidationResult result = await _registerValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Theory]
    [InlineData("AAPL")]
    [InlineData("MSFT")]
    [InlineData("BRK.A")]
    [InlineData("GOOGL")]
    public async Task WatchlistValidator_WithValidTicker_ShouldPass(string symbol)
    {
        // Arrange
        WatchlistAddRequest request = new WatchlistAddRequest(symbol);

        // Act
        ValidationResult result = await _watchlistValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("TOOLONGSYMBOLNAME")]
    [InlineData("SYM$BOL")]
    public async Task WatchlistValidator_WithInvalidSymbol_ShouldFail(string invalidSymbol)
    {
        // Arrange
        WatchlistAddRequest request = new WatchlistAddRequest(invalidSymbol);

        // Act
        ValidationResult result = await _watchlistValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
