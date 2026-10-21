using Core.DTOs;
using Core.Entities;

namespace Core.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    Task<int> CountUnreadAsync(int userId, CancellationToken ct = default);
    Task<HashSet<string>> GetExistingKeysAsync(IEnumerable<int> userIds, IEnumerable<string> keys, CancellationToken ct = default);
    Task<PaginatedResult<Notification>> GetPagedAsync(int userId, int page, int size, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(int userId, CancellationToken ct = default);
}
