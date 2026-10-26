namespace FinSightIQ.UserService.Core.DTOs;

/// <summary>
/// Database representation of a refresh token record with family tracking.
/// </summary>
public sealed record RefreshTokenRecordDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string TokenHash { get; init; } = string.Empty;
    public Guid FamilyId { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public bool IsUsed => UsedAt.HasValue;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsActive => !IsUsed && !IsRevoked && !IsExpired;

    public RefreshTokenRecordDto() { }

    public RefreshTokenRecordDto(
        Guid id,
        Guid userId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset expiresAt,
        DateTimeOffset? usedAt = null,
        DateTimeOffset? revokedAt = null,
        DateTimeOffset? createdAt = null)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
        UsedAt = usedAt;
        RevokedAt = revokedAt;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }
}
