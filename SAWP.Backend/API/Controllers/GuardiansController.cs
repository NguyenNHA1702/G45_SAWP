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

    #region Parent Child Linkage (Step 8 & 9)

    [HttpPost("link-requests")]
    [Authorize(Roles = "Parent")]
    [ProducesResponseType(typeof(ApiResponse<ChildLinkRequestResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChildLinkRequestResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ChildLinkRequestResultDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitChildLinkRequest([FromBody] SubmitChildLinkDto request, CancellationToken cancellationToken)
    {
        var parentUserId = User.GetUserId();
        if (!parentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<ChildLinkRequestResultDto>.Fail(SystemMessages.MSG07));
        }

        var result = await _guardianService.SubmitChildLinkRequestAsync(parentUserId.Value, request, cancellationToken);
        if (!result.Success)
        {
            if (result.Code == SystemMessages.MSG11.Code)
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("link-requests")]
    [Authorize(Roles = "OfficeStaff,Admin,SchoolBoard")]
    [ProducesResponseType(typeof(ApiResponse<List<ChildLinkRequestItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLinkRequests([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var result = await _guardianService.GetLinkRequestsAsync(status, cancellationToken);
        return Ok(result);
    }

    [HttpPost("link-requests/{id}/verify")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyChildLinkRequest(int id, [FromBody] VerifyChildLinkDto review, CancellationToken cancellationToken)
    {
        var reviewerUserId = User.GetUserId();
        if (!reviewerUserId.HasValue)
        {
            return Unauthorized(ApiResponse<bool>.Fail(SystemMessages.MSG07));
        }

        var result = await _guardianService.VerifyChildLinkRequestAsync(id, reviewerUserId.Value, review, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    #endregion

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
