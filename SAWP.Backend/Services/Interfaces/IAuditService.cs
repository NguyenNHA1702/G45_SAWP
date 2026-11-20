using Core.DTOs;
using Core.Entities;

namespace Services.Interfaces;

public interface IAuditService
{
    /// <summary>
    /// Stages an audit log entry on the shared DbContext. It does not save:
    /// the caller must call SaveChangesAsync afterwards, ideally together with the audited change.
    /// </summary>
    /// <param name="action">A constant from <see cref="Core.Constants.AuditActions"/>.</param>
    /// <param name="entityName">nameof() of the entity class that was changed.</param>
    /// <param name="entityId">Id of that entity, or null when the action covers several records.</param>
    /// <param name="oldValues">Values before the change (anonymous object or collection), or null.
    /// Serialized to JSON here; never pass a pre-built string.</param>
    /// <param name="newValues">Values after the change, same format as <paramref name="oldValues"/>.</param>
    /// <param name="ipAddress">Client IP address of the request. Not recorded yet: every caller passes null.</param>
    Task LogActionAsync(int? userId, string action, string entityName, string? entityId, object? oldValues, object? newValues, string? ipAddress, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginatedResult<AuditLog>>> GetAuditLogsPagedAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginatedResult<AuditLogDto>>> GetAuditLogDtosPagedAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);
}
