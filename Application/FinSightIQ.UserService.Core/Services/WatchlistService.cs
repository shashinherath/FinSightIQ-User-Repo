using FinSightIQ.BuildingBlocks.Results;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace FinSightIQ.UserService.Core.Services;

public sealed class WatchlistService : IWatchlistService
{
    private readonly IWatchlistRepository _watchlistRepository;
    private readonly IValidator<WatchlistAddRequest> _validator;
    private readonly ILogger<WatchlistService> _logger;

    public WatchlistService(
        IWatchlistRepository watchlistRepository,
        IValidator<WatchlistAddRequest> validator,
        ILogger<WatchlistService> logger)
    {
        _watchlistRepository = watchlistRepository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<WatchlistResponse>> GetWatchlistAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WatchlistItemDto> items = await _watchlistRepository.GetByUserIdAsync(userId, cancellationToken);

        List<string> symbols = items.Select(i => i.Symbol).ToList();
        List<WatchlistItemDto> dtos = items.Select(i => new WatchlistItemDto(i.Id, i.Symbol, i.AddedAt)).ToList();

        return Result<WatchlistResponse>.Success(new WatchlistResponse(symbols, dtos));
    }

    public async Task<Result<WatchlistItemDto>> AddSymbolAsync(
        Guid userId,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        WatchlistAddRequest request = new WatchlistAddRequest(symbol);
        ValidationResult validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<WatchlistItemDto>.Failure("VALIDATION_FAILED", validation.Errors[0].ErrorMessage);
        }

        string normalizedSymbol = symbol.Trim().ToUpperInvariant();
        WatchlistItemDto item = await _watchlistRepository.AddOrGetAsync(userId, normalizedSymbol, cancellationToken);

        _logger.LogInformation("Symbol {Symbol} added to watchlist for UserId={UserId}", normalizedSymbol, userId);

        return Result<WatchlistItemDto>.Success(new WatchlistItemDto(item.Id, item.Symbol, item.AddedAt));
    }

    public async Task<Result> RemoveSymbolAsync(
        Guid userId,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return Result.Failure("VALIDATION_FAILED", "Symbol is required.");
        }

        string normalizedSymbol = symbol.Trim().ToUpperInvariant();
        bool removed = await _watchlistRepository.RemoveAsync(userId, normalizedSymbol, cancellationToken);

        if (removed)
        {
            _logger.LogInformation("Symbol {Symbol} removed from watchlist for UserId={UserId}", normalizedSymbol, userId);
        }

        return Result.Success();
    }
}
