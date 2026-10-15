using Core.DTOs;
using Core.Entities;

namespace Services.Interfaces;

public interface IAuditService
{
    Task LogActionAsync(int? userId, string action, string entityName, string? entityId, string? oldValues, string? newValues, string? ipAddress, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginatedResult<AuditLog>>> GetAuditLogsPagedAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default);
}
