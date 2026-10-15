using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
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
        await Task.CompletedTask;
    }

    public async Task<ApiResponse<PaginatedResult<AuditLog>>> GetAuditLogsPagedAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<PaginatedResult<AuditLog>>.Ok(new PaginatedResult<AuditLog>());
    }
}
