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

public sealed class UsersControllerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly UsersController _sut;

    public UsersControllerTests()
    {
        _sut = new UsersController(_userService, NullLogger<UsersController>.Instance);
    }

    [Fact]
    public async Task GetUsers_WhenSuccessful_ShouldReturn200WithPagedUsers()
    {
        // Arrange
        PagedUsersResponse pagedResponse = new PagedUsersResponse(
            new List<UserAdminDto>
            {
                new(Guid.NewGuid(), "admin@example.com", "Administrator", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)
            },
            new PaginationMetadata(1, 50, 0, false));

        _userService.GetUsersAsync(50, 0, Arg.Any<CancellationToken>())
            .Returns(Result<PagedUsersResponse>.Success(pagedResponse));

        // Act
        IActionResult result = await _sut.GetUsers(50, 0, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().Be(pagedResponse);
    }

    [Fact]
    public async Task SuspendUser_WithEmptyGuid_ShouldReturn400BadRequest()
    {
        // Act
        IActionResult result = await _sut.SuspendUser(Guid.Empty, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SuspendUser_WithValidGuid_WhenServiceSucceeds_ShouldReturn200()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        _userService.SuspendUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        IActionResult result = await _sut.SuspendUser(userId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ReinstateUser_WhenUserNotFound_ShouldReturn404NotFound()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        _userService.ReinstateUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("RESOURCE_NOT_FOUND", "User not found.")));

        // Act
        IActionResult result = await _sut.ReinstateUser(userId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        ObjectResult objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
