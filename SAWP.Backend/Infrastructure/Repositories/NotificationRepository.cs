using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<int> CountUnreadAsync(int userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task<HashSet<string>> GetExistingKeysAsync(IEnumerable<int> userIds, IEnumerable<string> keys, CancellationToken ct = default)
    {
        var uIds = userIds.ToList();
        var kList = keys.ToList();

        var matches = await _context.Notifications
            .AsNoTracking()
            .Where(n => uIds.Contains(n.UserId) && n.ReferenceKey != null && kList.Contains(n.ReferenceKey))
            .Select(n => n.UserId + "|" + n.ReferenceKey)
            .ToListAsync(ct);

        return matches.ToHashSet();
    }

    public async Task<PaginatedResult<Notification>> GetPagedAsync(int userId, int page, int size, CancellationToken ct = default)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        return new PaginatedResult<Notification>
        {
            Items = items,
            TotalCount = total,
            PageIndex = page,
            PageSize = size
        };
    }

    public async Task<int> MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
