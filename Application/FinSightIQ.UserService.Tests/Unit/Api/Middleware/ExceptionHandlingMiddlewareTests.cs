using System.Net;
using System.Text.Json;
using FinSightIQ.UserService.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinSightIQ.UserService.Tests.Unit.Api.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenNoException_ShouldProceedNormally()
    {
        // Arrange
        DefaultHttpContext context = new DefaultHttpContext();
        bool nextCalled = false;
        ExceptionHandlingMiddleware middleware = new ExceptionHandlingMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentException_ShouldReturn400()
    {
        // Arrange
        DefaultHttpContext context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        ExceptionHandlingMiddleware middleware = new ExceptionHandlingMiddleware(
            _ => throw new ArgumentException("Invalid argument provided"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using StreamReader reader = new StreamReader(context.Response.Body);
        string body = await reader.ReadToEndAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("error").GetString().Should().Be("Invalid argument provided");
    }

    [Fact]
    public async Task InvokeAsync_WhenKeyNotFoundException_ShouldReturn404()
    {
        // Arrange
        DefaultHttpContext context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        ExceptionHandlingMiddleware middleware = new ExceptionHandlingMiddleware(
            _ => throw new KeyNotFoundException("Resource not found"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_ShouldReturn500()
    {
        // Arrange
        DefaultHttpContext context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        ExceptionHandlingMiddleware middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Unexpected error"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
    }
}
