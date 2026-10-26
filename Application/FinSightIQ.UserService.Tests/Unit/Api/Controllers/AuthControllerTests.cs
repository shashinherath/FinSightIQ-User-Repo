using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Api.Controllers;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using FinSightIQ.UserService.Core.Validators;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinSightIQ.UserService.Tests.Unit.Api.Controllers;

public sealed class AuthControllerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IValidator<RegisterRequest> _registerValidator = new RegisterRequestValidator();
    private readonly IValidator<LoginRequest> _loginValidator = new LoginRequestValidator();
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator = new ChangePasswordRequestValidator();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

    private readonly AuthController _sut;
    private readonly DefaultHttpContext _httpContext;

    public AuthControllerTests()
    {
        _sut = new AuthController(
            _userService,
            _registerValidator,
            _loginValidator,
            _changePasswordValidator,
            _configuration,
            NullLogger<AuthController>.Instance);

        _httpContext = new DefaultHttpContext();
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };
    }

    [Fact]
    public async Task Register_WithValidRequest_WhenServiceSucceeds_ShouldReturn201Created()
    {
        // Arrange
        RegisterRequest request = new RegisterRequest("analyst@example.com", "Str0ngPassphrase!");
        RegisterResponse expectedResponse = new RegisterResponse(Guid.NewGuid(), "analyst@example.com", "Analyst", DateTimeOffset.UtcNow);

        _userService.RegisterAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result<RegisterResponse>.Success(expectedResponse));

        // Act
        IActionResult result = await _sut.Register(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        ObjectResult objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        objResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task Register_WithInvalidPassword_ShouldReturn400ValidationFailed()
    {
        // Arrange
        RegisterRequest request = new RegisterRequest("analyst@example.com", "weak");

        // Act
        IActionResult result = await _sut.Register(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        ObjectResult objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        await _userService.DidNotReceiveWithAnyArgs().RegisterAsync(default!, default);
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyRegistered_ShouldReturn409Conflict()
    {
        // Arrange
        RegisterRequest request = new RegisterRequest("existing@example.com", "Str0ngPassphrase!");
        _userService.RegisterAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result<RegisterResponse>.Failure(new Error("EMAIL_ALREADY_REGISTERED", "Email is already taken.")));

        // Act
        IActionResult result = await _sut.Register(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        ObjectResult objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldSetCookieAndReturn200()
    {
        // Arrange
        LoginRequest request = new LoginRequest("analyst@example.com", "Str0ngPassphrase!");
        UserDto userDto = new UserDto(Guid.NewGuid(), "analyst@example.com", "Analyst");
        LoginResponse loginResponse = new LoginResponse("mock.jwt.token", "Bearer", 900, userDto);

        _userService.LoginAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result<(LoginResponse Response, string RawRefreshToken)>.Success((loginResponse, "raw-refresh-token-xyz")));

        // Act
        IActionResult result = await _sut.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().Be(loginResponse);

        _httpContext.Response.Headers.ContainsKey("Set-Cookie").Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_WhenCookieMissing_ShouldReturn401Unauthenticated()
    {
        // Act (no cookie set)
        IActionResult result = await _sut.Refresh(CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        ObjectResult objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Logout_ShouldReturn204NoContent()
    {
        // Act
        IActionResult result = await _sut.Logout(CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }
}
