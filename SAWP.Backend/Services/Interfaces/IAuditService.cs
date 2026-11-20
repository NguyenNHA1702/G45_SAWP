using Core.DTOs;
using Core.Entities;

namespace Services.Interfaces;

public interface IAuditService
{
    /// <summary>
    /// Stages an audit log entry on the shared DbContext. It does not save:
    /// the caller must call SaveChangesAsync afterwards, ideally together with the audited change.
    /// </summary>
    Task LogActionAsync(int? userId, string action, string entityName, string? entityId, string? oldValues, string? newValues, string? ipAddress, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginatedResult<AuditLog>>> GetAuditLogsPagedAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginatedResult<AuditLogDto>>> GetAuditLogDtosPagedAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);
}
