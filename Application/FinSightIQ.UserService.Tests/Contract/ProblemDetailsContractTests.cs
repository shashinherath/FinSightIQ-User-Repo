using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinSightIQ.UserService.Api;
using FinSightIQ.UserService.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinSightIQ.UserService.Tests.Contract;

public sealed class ProblemDetailsContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProblemDetailsContractTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidationFailure_ShouldReturnRfc7807ProblemDetails()
    {
        // Arrange: Invalid request (password too short)
        RegisterRequest payload = new RegisterRequest("test@example.com", "short");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/auth/register", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(content);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("type", out _).Should().BeTrue();
        root.TryGetProperty("title", out _).Should().BeTrue();
        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
        root.TryGetProperty("detail", out _).Should().BeTrue();
        root.TryGetProperty("traceId", out _).Should().BeTrue();
        root.TryGetProperty("errors", out JsonElement errors).Should().BeTrue();
        errors.TryGetProperty("password", out _).Should().BeTrue();
    }

    [Fact]
    public async Task MissingTokenOnProtectedEndpoint_ShouldReturn401ProblemDetails()
    {
        // Act: Call protected endpoint without token
        HttpResponseMessage response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(content);
        JsonElement root = doc.RootElement;

        root.GetProperty("code").GetString().Should().Be("UNAUTHENTICATED");
        root.GetProperty("status").GetInt32().Should().Be(401);
    }
}
