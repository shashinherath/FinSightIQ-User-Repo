using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Api.Extensions;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSightIQ.UserService.Api.Controllers;

[ApiController]
[Route("api/v1/watchlist")]
[Route("api/v1/watchlists")]
[Authorize]
public class WatchlistController : ControllerBase
{
    private readonly IWatchlistService _watchlistService;
    private readonly ILogger<WatchlistController> _logger;

    public WatchlistController(IWatchlistService watchlistService, ILogger<WatchlistController> logger)
    {
        _watchlistService = watchlistService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(WatchlistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWatchlist(CancellationToken cancellationToken)
    {
        Guid? currentUserId = User.GetUserId();
        if (currentUserId == null)
        {
            _logger.LogWarning("Unauthorized attempt to access watchlist.");
            return this.ToProblemResult(new Error("UNAUTHENTICATED", "Authentication is required."));
        }

        _logger.LogInformation("Fetching watchlist for UserId={UserId}", currentUserId.Value);

        Result<WatchlistResponse> result = await _watchlistService.GetWatchlistAsync(currentUserId.Value, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Get watchlist failed for UserId {UserId}: {Code}", currentUserId.Value, result.Error.Code);
            return this.ToProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WatchlistItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddSymbol(
        [FromBody] WatchlistAddRequest request,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = User.GetUserId();
        if (currentUserId == null)
        {
            _logger.LogWarning("Unauthorized attempt to add symbol to watchlist.");
            return this.ToProblemResult(new Error("UNAUTHENTICATED", "Authentication is required."));
        }

        if (string.IsNullOrWhiteSpace(request?.Symbol))
        {
            _logger.LogWarning("Invalid symbol provided for UserId={UserId}", currentUserId.Value);
            return BadRequest("Symbol is required.");
        }

        _logger.LogInformation("Adding symbol {Symbol} to watchlist for UserId={UserId}", request.Symbol, currentUserId.Value);

        Result<WatchlistItemDto> result = await _watchlistService.AddSymbolAsync(currentUserId.Value, request.Symbol, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Add symbol {Symbol} failed for UserId {UserId}: {Code}",
                request.Symbol, currentUserId.Value, result.Error.Code);
            return this.ToProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{symbol}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveSymbol(
        [FromRoute] string symbol,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = User.GetUserId();
        if (currentUserId == null)
        {
            _logger.LogWarning("Unauthorized attempt to remove symbol from watchlist.");
            return this.ToProblemResult(new Error("UNAUTHENTICATED", "Authentication is required."));
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            _logger.LogWarning("Empty symbol provided for deletion for UserId={UserId}", currentUserId.Value);
            return BadRequest("Symbol is required.");
        }

        _logger.LogInformation("Removing symbol {Symbol} from watchlist for UserId={UserId}", symbol, currentUserId.Value);

        Result result = await _watchlistService.RemoveSymbolAsync(currentUserId.Value, symbol, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Remove symbol {Symbol} failed for UserId {UserId}: {Code}",
                symbol, currentUserId.Value, result.Error.Code);
            return this.ToProblemResult(result.Error);
        }

        return NoContent();
    }
}
