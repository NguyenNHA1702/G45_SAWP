using API.Extensions;
using Core.DTOs;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    /// <summary>
    /// Lấy danh sách điểm danh của lớp theo ngày và buổi học (Sáng/Chiều), tự động tích hợp đơn nghỉ đã duyệt
    /// </summary>
    [HttpGet("class/{classId}/sheet")]
    public async Task<IActionResult> GetClassDailySheet(
        int classId,
        [FromQuery] DateTime? date,
        [FromQuery] AttendanceSession session = AttendanceSession.Morning,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<ClassDailySheetDto>.Fail("Vui lòng đăng nhập lại."));
        }

        var targetDate = date ?? DateTime.Today;
        var result = await _attendanceService.GetClassDailySheetAsync(classId, targetDate, session, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// GVCN lưu điểm danh thực tế theo buổi, kiểm tra xung đột với đơn nghỉ và kích hoạt hủy suất ăn nếu vắng trước 08h15
    /// </summary>
    [HttpPost("class/save")]
    public async Task<IActionResult> SaveClassAttendance([FromBody] SaveClassAttendanceDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<AttendanceSaveResultDto>.Fail("Vui lòng đăng nhập lại."));
        }

        var result = await _attendanceService.SaveClassAttendanceAsync(request, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
