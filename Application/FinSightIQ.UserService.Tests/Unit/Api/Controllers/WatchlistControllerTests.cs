using System.Security.Claims;
using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Api.Controllers;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinSightIQ.UserService.Tests.Unit.Api.Controllers;

public sealed class WatchlistControllerTests
{
    private readonly IWatchlistService _watchlistService = Substitute.For<IWatchlistService>();
    private readonly WatchlistController _sut;
    private readonly DefaultHttpContext _httpContext;

    public WatchlistControllerTests()
    {
        _sut = new WatchlistController(_watchlistService, NullLogger<WatchlistController>.Instance);
        _httpContext = new DefaultHttpContext();
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };
    }

    [Fact]
    public async Task GetWatchlist_WhenUserUnauthenticated_ShouldReturn401()
    {
        // Act (User has no sub/nameidentifier claim)
        IActionResult result = await _sut.GetWatchlist(CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        ObjectResult objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetWatchlist_WhenAuthenticated_ShouldReturn200WithWatchlist()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        SetUserPrincipal(userId);

        WatchlistResponse expectedResponse = new WatchlistResponse(
            new[] { "AAPL" },
            new List<WatchlistItemDto>
            {
                new(Guid.NewGuid(), "AAPL", DateTimeOffset.UtcNow)
            });

        _watchlistService.GetWatchlistAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result<WatchlistResponse>.Success(expectedResponse));

        // Act
        IActionResult result = await _sut.GetWatchlist(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task AddSymbol_WithEmptySymbol_ShouldReturn400BadRequest()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        SetUserPrincipal(userId);

        WatchlistAddRequest request = new WatchlistAddRequest("");

        // Act
        IActionResult result = await _sut.AddSymbol(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddSymbol_WithValidSymbol_ShouldReturn200Ok()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        SetUserPrincipal(userId);

        WatchlistAddRequest request = new WatchlistAddRequest("TSLA");
        WatchlistItemDto itemDto = new WatchlistItemDto(Guid.NewGuid(), "TSLA", DateTimeOffset.UtcNow);

        _watchlistService.AddSymbolAsync(userId, "TSLA", Arg.Any<CancellationToken>())
            .Returns(Result<WatchlistItemDto>.Success(itemDto));

        // Act
        IActionResult result = await _sut.AddSymbol(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().Be(itemDto);
    }

    [Fact]
    public async Task RemoveSymbol_WhenFound_ShouldReturn204NoContent()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        SetUserPrincipal(userId);

        _watchlistService.RemoveSymbolAsync(userId, "MSFT", Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        IActionResult result = await _sut.RemoveSymbol("MSFT", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    private void SetUserPrincipal(Guid userId)
    {
        List<Claim> claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "Analyst")
        };
        ClaimsIdentity identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);
    }
}
