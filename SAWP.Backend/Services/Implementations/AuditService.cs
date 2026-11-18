using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class AuditService : IAuditService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task LogActionAsync(int? userId, string action, string entityName, string? entityId, string? oldValues, string? newValues, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        };
        await _unitOfWork.Repository<AuditLog>().AddAsync(log, cancellationToken);
    }

    public async Task<ApiResponse<PaginatedResult<AuditLog>>> GetAuditLogsPagedAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, pageIndex);
        var size = Math.Clamp(pageSize, 1, 200);
        var query = _unitOfWork.Repository<AuditLog>().Query().OrderByDescending(x => x.Timestamp);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);

        return ApiResponse<PaginatedResult<AuditLog>>.Ok(new PaginatedResult<AuditLog>
        {
            Items = items,
            TotalCount = total,
            PageIndex = page,
            PageSize = size
        });
    }

    public async Task<ApiResponse<PaginatedResult<AuditLogDto>>> GetAuditLogDtosPagedAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.PageIndex);
        var size = Math.Clamp(filter.PageSize, 1, 100);

        var query = _unitOfWork.Repository<AuditLog>()
            .Query()
            .Include(x => x.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim().ToLower();
            query = query.Where(x => x.Action.ToLower().Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            var entity = filter.EntityName.Trim().ToLower();
            query = query.Where(x => x.EntityName.ToLower().Contains(entity));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var q = filter.Search.Trim().ToLower();
            query = query.Where(x =>
                x.Action.ToLower().Contains(q) ||
                x.EntityName.ToLower().Contains(q) ||
                (x.EntityId != null && x.EntityId.ToLower().Contains(q)) ||
                (x.NewValues != null && x.NewValues.ToLower().Contains(q)) ||
                (x.User != null && (x.User.Username.ToLower().Contains(q) || x.User.FullName.ToLower().Contains(q))));
        }

        query = query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                UserId = x.UserId,
                Username = x.User != null ? x.User.Username : null,
                UserFullName = x.User != null ? x.User.FullName : null,
                Action = x.Action,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                OldValues = x.OldValues,
                NewValues = x.NewValues,
                IpAddress = x.IpAddress,
                Timestamp = x.Timestamp
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<PaginatedResult<AuditLogDto>>.Ok(new PaginatedResult<AuditLogDto>(items, total, page, size));
    }
}
