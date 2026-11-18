using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : BaseApiController
{
    private readonly IAuditService _auditService;

    public SystemController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet("audit-logs")]
    [Authorize(Roles = "Admin,SchoolBoard,OfficeStaff")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? action = null,
        [FromQuery] string? entityName = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var filter = new AuditLogFilterDto
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Action = action,
            EntityName = entityName,
            Search = search
        };
        var result = await _auditService.GetAuditLogDtosPagedAsync(filter, ct);
        return ToActionResult(result);
    }
}
