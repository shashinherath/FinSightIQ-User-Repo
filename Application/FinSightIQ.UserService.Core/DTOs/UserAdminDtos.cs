namespace FinSightIQ.UserService.Core.DTOs;

/// <summary>User account details presented to administrators (excludes password hash).</summary>
public sealed record UserAdminDto(
    Guid Id,
    string Email,
    string Role,
    bool IsSuspended,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);

/// <summary>Pagination envelope metadata.</summary>
public sealed record PaginationMetadata(
    int Total,
    int Limit,
    int Offset,
    bool HasMore);

/// <summary>Paginated collection of users for administrative listings.</summary>
public sealed record PagedUsersResponse(
    IReadOnlyList<UserAdminDto> Data,
    PaginationMetadata Pagination);
