using FinSightIQ.UserService.Core.DTOs;

namespace FinSightIQ.UserService.Core.Interfaces;

/// <summary>
/// Data persistence operations for user records.
/// </summary>
public interface IUserRepository
{
    Task<UserRecordDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserRecordDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserRecordDto> CreateAsync(UserRecordDto user, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserRecordDto user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserRecordDto>> GetPagedAsync(int limit, int offset, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
