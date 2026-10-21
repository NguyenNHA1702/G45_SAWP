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
        await Task.CompletedTask;
        return ApiResponse<PaginatedResult<AuditLog>>.Ok(new PaginatedResult<AuditLog>());
    }
}
