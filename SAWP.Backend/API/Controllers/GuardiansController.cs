using API.Extensions;
using Core.Constants;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GuardiansController : ControllerBase
{
    private readonly IGuardianService _guardianService;

    public GuardiansController(IGuardianService guardianService)
    {
        _guardianService = guardianService;
    }

    #region Parent Portal Dashboard & Class Information (Step 10)

    [HttpGet("my-children")]
    [Authorize(Roles = "Parent")]
    [ProducesResponseType(typeof(ApiResponse<List<LinkedChildSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyChildren(CancellationToken cancellationToken)
    {
        var parentUserId = User.GetUserId();
        if (!parentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<List<LinkedChildSummaryDto>>.Fail(SystemMessages.MSG07));
        }

        var result = await _guardianService.GetMyLinkedChildrenAsync(parentUserId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpGet("my-children/{studentId}/dashboard")]
    [Authorize(Roles = "Parent")]
    [ProducesResponseType(typeof(ApiResponse<ChildDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChildDashboardDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ChildDashboardDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildDashboard(int studentId, CancellationToken cancellationToken)
    {
        var parentUserId = User.GetUserId();
        if (!parentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<ChildDashboardDto>.Fail(SystemMessages.MSG07));
        }

        var result = await _guardianService.GetChildDashboardAsync(parentUserId.Value, studentId, cancellationToken);
        if (!result.Success)
        {
            if (result.Code == SystemMessages.MSG10.Code)
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            return NotFound(result);
        }

        return Ok(result);
    }

    #endregion
}
