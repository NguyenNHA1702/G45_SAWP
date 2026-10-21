using API.Extensions;
using Core.DTOs;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttendanceController : BaseApiController
{
    private readonly IAttendanceService _attendanceService;
    private readonly ISchoolClock _clock;

    public AttendanceController(IAttendanceService attendanceService, ISchoolClock clock)
    {
        _attendanceService = attendanceService;
        _clock = clock;
    }

    /// <summary>
    /// API-1: Giáo viên lấy danh sách lớp được phân công điểm danh
    /// </summary>
    [HttpGet("my-classes")]
    [HttpGet("classes")]
    [Authorize(Roles = "Teacher,OfficeStaff,Admin,SchoolBoard")]
    public async Task<IActionResult> GetMyClasses([FromQuery] DateTime? date, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _attendanceService.GetMyClassesAsync(userId, roles, date, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// API-2: Tải bảng điểm danh chi tiết của lớp theo ngày
    /// </summary>
    [HttpGet("classes/{classId}/sheet")]
    [HttpGet("classes/{classId}")]
    [Authorize(Roles = "Teacher,OfficeStaff,SchoolBoard,Admin")]
    public async Task<IActionResult> GetAttendanceSheet(int classId, [FromQuery] DateTime? date, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var targetDate = date ?? _clock.Today;
        var result = await _attendanceService.GetAttendanceSheetAsync(classId, targetDate, userId, roles, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// API-3: Nộp / Lưu điểm danh lớp trong ngày (Idempotent)
    /// </summary>
    [HttpPut("classes/{classId}/sheet")]
    [HttpPut("classes/{classId}")]
    [HttpPost("classes/{classId}")]
    [Authorize(Roles = "Teacher,OfficeStaff,Admin")]
    public async Task<IActionResult> SubmitAttendanceSheet(int classId, [FromBody] SubmitAttendanceSheetDto dto, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _attendanceService.RecordDailyAttendanceAsync(classId, dto, userId, roles, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// API-4: Giáo vụ sửa bản ghi điểm danh kèm lý do bắt buộc
    /// </summary>
    [HttpPatch("records/{recordId}/correction")]
    [HttpPut("records/{recordId}")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    public async Task<IActionResult> CorrectAttendance(int recordId, [FromBody] CorrectAttendanceDto dto, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var result = await _attendanceService.CorrectAttendanceAsync(recordId, dto, userId, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// API-5: Báo cáo chuyên cần tổng hợp của lớp
    /// </summary>
    [HttpGet("classes/{classId}/summary")]
    [Authorize(Roles = "Teacher,OfficeStaff,SchoolBoard,Admin")]
    public async Task<IActionResult> GetClassSummary(
        int classId, 
        [FromQuery] DateTime? from, 
        [FromQuery] DateTime? to, 
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var fromDate = from ?? _clock.Today.AddDays(-30);
        var toDate = to ?? _clock.Today;
        var result = await _attendanceService.GetClassSummaryAsync(classId, fromDate, toDate, userId, roles, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// API-6: Lịch sử điểm danh chi tiết của một học sinh
    /// </summary>
    [HttpGet("students/{studentId}/history")]
    [Authorize(Roles = "Parent,Teacher,OfficeStaff,SchoolBoard,Admin")]
    public async Task<IActionResult> GetStudentHistory(
        int studentId, 
        [FromQuery] DateTime? from, 
        [FromQuery] DateTime? to, 
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var fromDate = from ?? _clock.Today.AddDays(-30);
        var toDate = to ?? _clock.Today;
        var result = await _attendanceService.GetStudentHistoryAsync(studentId, fromDate, toDate, userId, roles, ct);
        return ToActionResult(result);
    }
}
