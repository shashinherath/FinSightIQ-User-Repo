using FinSightIQ.BuildingBlocks.Middleware;
using FinSightIQ.UserService.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FinSightIQ.UserService.Tests.Unit.Api.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenHeaderProvided_ShouldUseProvidedCorrelationId()
    {
        // Arrange
        string expectedId = Guid.NewGuid().ToString();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Headers[CorrelationId.HeaderName] = expectedId;

        bool nextCalled = false;
        CorrelationIdMiddleware middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.Headers[CorrelationId.HeaderName].ToString().Should().Be(expectedId);
        context.Items[CorrelationId.HeaderName].Should().Be(expectedId);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderNotProvided_ShouldGenerateNewCorrelationId()
    {
        // Arrange
        DefaultHttpContext context = new DefaultHttpContext();

        bool nextCalled = false;
        CorrelationIdMiddleware middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.Headers[CorrelationId.HeaderName].ToString().Should().NotBeNullOrWhiteSpace();
        context.Items[CorrelationId.HeaderName].Should().NotBeNull();
    }
}
