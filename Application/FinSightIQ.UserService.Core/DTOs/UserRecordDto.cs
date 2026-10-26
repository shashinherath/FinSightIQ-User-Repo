namespace FinSightIQ.UserService.Core.DTOs;

/// <summary>
/// Database representation of a user record.
/// </summary>
public sealed record UserRecordDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public UserRole Role { get; init; } = UserRole.Viewer;
    public bool IsSuspended { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }

    public bool IsActive => !IsSuspended && DeletedAt == null;

    public UserRecordDto() { }

    public UserRecordDto(
        Guid id,
        string email,
        string passwordHash,
        UserRole role,
        bool isSuspended,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? deletedAt = null)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsSuspended = isSuspended;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
    }
}
