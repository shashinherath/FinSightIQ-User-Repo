using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Api.Extensions;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSightIQ.UserService.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Administrator")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedUsersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching paged users: Limit={Limit}, Offset={Offset}", limit, offset);

        Result<PagedUsersResponse> result = await _userService.GetUsersAsync(limit, offset, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Get users failed: {Code} - {Message}", result.Error.Code, result.Error.Message);
            return this.ToProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendUser(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            _logger.LogWarning("Invalid user ID provided for suspension.");
            return BadRequest("Invalid user ID provided.");
        }

        _logger.LogInformation("Suspending user with ID: {UserId}", id);

        Result result = await _userService.SuspendUserAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Suspend failed for UserId {UserId}: {Code}", id, result.Error.Code);
            return this.ToProblemResult(result.Error);
        }

        _logger.LogInformation("User {UserId} suspended successfully.", id);
        return Ok(new { message = "User account suspended successfully." });
    }

    [HttpPost("{id:guid}/reinstate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReinstateUser(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            _logger.LogWarning("Invalid user ID provided for reinstatement.");
            return BadRequest("Invalid user ID provided.");
        }

        _logger.LogInformation("Reinstating user with ID: {UserId}", id);

        Result result = await _userService.ReinstateUserAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Reinstate failed for UserId {UserId}: {Code}", id, result.Error.Code);
            return this.ToProblemResult(result.Error);
        }

        _logger.LogInformation("User {UserId} reinstated successfully.", id);
        return Ok(new { message = "User account reinstated successfully." });
    }
}
