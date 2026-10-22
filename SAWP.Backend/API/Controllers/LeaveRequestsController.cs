using API.Extensions;
using Core.DTOs;
using Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using System.IO;

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

    /// <summary>
    /// Tải lên tệp đính kèm (giấy khám bệnh, minh chứng)
    /// </summary>
    [HttpPost("upload-attachment")]
    public async Task<IActionResult> UploadAttachment(
        IFormFile file,
        [FromServices] IFileStorageService fileStorageService,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<AttachmentUploadResultDto>.Fail("Vui lòng chọn tệp tin cần tải lên."));
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<AttachmentUploadResultDto>.Fail("Kích thước tệp không được vượt quá 10MB."));
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse<AttachmentUploadResultDto>.Fail("Định dạng tệp không được hỗ trợ. Vui lòng chọn ảnh (.jpg, .png) hoặc tài liệu (.pdf, .doc, .docx)."));
        }

        using var stream = file.OpenReadStream();
        var relativePath = await fileStorageService.SaveFileAsync(stream, file.FileName, "attachments", cancellationToken);
        var normalizedPath = relativePath.Replace('\\', '/');
        var fileUrl = $"/api/LeaveRequests/attachment-file?path={Uri.EscapeDataString(normalizedPath)}";

        return Ok(ApiResponse<AttachmentUploadResultDto>.Ok(new AttachmentUploadResultDto
        {
            Url = fileUrl,
            FileName = file.FileName,
            FileSize = file.Length,
            SavedPath = normalizedPath
        }, "Tải lên tệp đính kèm thành công."));
    }

    /// <summary>
    /// Tải / Xem tệp đính kèm
    /// </summary>
    [HttpGet("attachment-file")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAttachmentFile(
        [FromQuery] string path,
        [FromServices] IFileStorageService fileStorageService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest();
        }

        var cleanPath = path.Replace("..", "").TrimStart('/', '\\');
        var stream = await fileStorageService.GetFileAsync(cleanPath, cancellationToken);
        if (stream == null)
        {
            return NotFound("Không tìm thấy tệp đính kèm.");
        }

        var ext = Path.GetExtension(cleanPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };

        return File(stream, contentType, Path.GetFileName(cleanPath));
    }
}
