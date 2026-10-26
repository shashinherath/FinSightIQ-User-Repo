namespace FinSightIQ.UserService.Core.DTOs;

/// <summary>Inbound payload for adding a symbol to the user watchlist.</summary>
public sealed record WatchlistAddRequest(string Symbol);

/// <summary>Watchlist entry item DTO.</summary>
public sealed record WatchlistItemDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public DateTimeOffset AddedAt { get; init; }

    public WatchlistItemDto() { }

    public WatchlistItemDto(Guid id, Guid userId, string symbol, DateTimeOffset addedAt)
    {
        Id = id;
        UserId = userId;
        Symbol = symbol;
        AddedAt = addedAt;
    }

    public WatchlistItemDto(Guid id, string symbol, DateTimeOffset addedAt)
        : this(id, Guid.Empty, symbol, addedAt)
    {
    }
}

/// <summary>Outbound response containing user watchlist entries.</summary>
public sealed record WatchlistResponse(
    IReadOnlyList<string> Symbols,
    IReadOnlyList<WatchlistItemDto> Data);
