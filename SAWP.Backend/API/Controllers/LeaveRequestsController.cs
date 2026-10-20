using API.Extensions;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaveRequestsController : ControllerBase
{
    private readonly ILeaveRequestService _leaveRequestService;

    public LeaveRequestsController(ILeaveRequestService leaveRequestService)
    {
        _leaveRequestService = leaveRequestService;
    }

    /// <summary>
    /// Phụ huynh nộp đơn xin nghỉ học cho con
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitLeaveRequest([FromBody] CreateLeaveRequestDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<LeaveRequestItemDto>.Fail("Vui lòng đăng nhập lại."));
        }

        var result = await _leaveRequestService.SubmitLeaveRequestAsync(userId.Value, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Phụ huynh xem danh sách đơn xin nghỉ của con
    /// </summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyLeaveRequests(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<List<LeaveRequestItemDto>>.Fail("Vui lòng đăng nhập lại."));
        }

        var result = await _leaveRequestService.GetMyLeaveRequestsAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GVCN xem danh sách đơn xin nghỉ học của lớp
    /// </summary>
    [HttpGet("class")]
    public async Task<IActionResult> GetClassLeaveRequests(
        [FromQuery] int? classId,
        [FromQuery] string? status,
        [FromQuery] DateTime? date,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<List<LeaveRequestItemDto>>.Fail("Vui lòng đăng nhập lại."));
        }

        var result = await _leaveRequestService.GetClassLeaveRequestsAsync(userId.Value, classId, status, date, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết một đơn xin nghỉ
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetLeaveRequestDetails(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<LeaveRequestItemDto>.Fail("Vui lòng đăng nhập lại."));
        }

        var result = await _leaveRequestService.GetLeaveRequestDetailsAsync(id, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// GVCN duyệt (Approve) hoặc từ chối (Reject) đơn xin nghỉ kèm đồng bộ bán trú & điểm danh
    /// </summary>
    [HttpPut("{id}/review")]
    public async Task<IActionResult> ReviewLeaveRequest(int id, [FromBody] ReviewLeaveRequestDto review, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<SyncLeaveResultDto>.Fail("Vui lòng đăng nhập lại."));
        }

        var result = await _leaveRequestService.ReviewLeaveRequestAsync(id, userId.Value, review, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Phụ huynh hủy đơn khi còn ở trạng thái Chờ duyệt
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelLeaveRequest(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Vui lòng đăng nhập lại."));
        }

        var result = await _leaveRequestService.CancelLeaveRequestAsync(id, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
