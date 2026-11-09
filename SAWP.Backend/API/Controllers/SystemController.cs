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
    public async Task<IActionResult> GetAuditLogs([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _auditService.GetAuditLogsPagedAsync(pageIndex, pageSize, ct);
        return ToActionResult(result);
    }
}
