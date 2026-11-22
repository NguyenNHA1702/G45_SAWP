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
    [Authorize(Roles = "Teacher,SchoolBoard,Admin")]
    public async Task<IActionResult> ReviewLeaveRequest(int id, [FromBody] ReviewLeaveRequestDto review, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<SyncLeaveResultDto>.Fail("Vui lòng đăng nhập lại."));
        }

        var roles = User.GetRoles();
        var result = await _leaveRequestService.ReviewLeaveRequestAsync(id, userId.Value, review, roles, cancellationToken);
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

    /// <summary>
    /// Tải lên tệp đính kèm (giấy khám bệnh, minh chứng)
    /// </summary>
    [HttpPost("upload-attachment")]
    public async Task<IActionResult> UploadAttachment(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<AttachmentUploadResultDto>.Fail("Vui lòng chọn tệp tin cần tải lên."));
        }

        using var stream = file.OpenReadStream();
        var result = await _leaveRequestService.UploadAttachmentAsync(stream, file.FileName, file.Length, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Tải / Xem tệp đính kèm
    /// </summary>
    [HttpGet("attachment-file")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAttachmentFile([FromQuery] string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest();
        }

        var attachment = await _leaveRequestService.GetAttachmentAsync(path, cancellationToken);
        if (attachment == null)
        {
            return NotFound("Không tìm thấy tệp đính kèm.");
        }

        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }
}
