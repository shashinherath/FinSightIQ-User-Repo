using System.Security.Claims;
using FinSightIQ.UserService.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FinSightIQ.UserService.Tests.Unit.Api.Middleware;

public sealed class UserContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithTrustedGatewayHeaders_ShouldAuthenticateUserWithClaims()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = userId.ToString();
        context.Request.Headers["X-User-Role"] = "Administrator";

        bool nextCalled = false;
        UserContextMiddleware middleware = new UserContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.User.Identity?.IsAuthenticated.Should().BeTrue();
        context.User.FindFirst("sub")?.Value.Should().Be(userId.ToString());
        context.User.FindFirst("role")?.Value.Should().Be("Administrator");
        context.User.IsInRole("Administrator").Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithoutHeaders_ShouldLeaveUserUnauthenticated()
    {
        // Arrange
        DefaultHttpContext context = new DefaultHttpContext();

        bool nextCalled = false;
        UserContextMiddleware middleware = new UserContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        (context.User.Identity?.IsAuthenticated ?? false).Should().BeFalse();
    }
}
