using API.Extensions;
using Core.DTOs;
using Core.Enums;
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
    public async Task<IActionResult> GetMyClasses(
        [FromQuery] DateTime? date,
        [FromQuery] AttendanceSession session = AttendanceSession.Morning,
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _attendanceService.GetMyClassesAsync(userId, roles, date, session, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// API-2: Tải bảng điểm danh chi tiết của lớp theo ngày và buổi (Sáng/Chiều)
    /// </summary>
    [HttpGet("classes/{classId}/sheet")]
    [HttpGet("classes/{classId}")]
    [Authorize(Roles = "Teacher,OfficeStaff,SchoolBoard,Admin")]
    public async Task<IActionResult> GetAttendanceSheet(
        int classId,
        [FromQuery] DateTime? date,
        [FromQuery] AttendanceSession session = AttendanceSession.Morning,
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var targetDate = date ?? _clock.Today;
        var result = await _attendanceService.GetAttendanceSheetAsync(classId, targetDate, session, userId, roles, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// API-3: submit / save the attendance of a class for a date and session (idempotent).
    /// Teacher (UC-21) or Office Staff (UC-22 A1); the System Administrator is not an actor (OVR-10).
    /// </summary>
    [HttpPut("classes/{classId}/sheet")]
    [HttpPut("classes/{classId}")]
    [HttpPost("classes/{classId}")]
    [Authorize(Roles = "Teacher,OfficeStaff")]
    public async Task<IActionResult> SubmitAttendanceSheet(int classId, [FromBody] SubmitAttendanceSheetDto dto, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _attendanceService.RecordDailyAttendanceAsync(classId, dto, userId, roles, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// API-4: Office Staff correct an attendance record with a mandatory reason (UC-22; Office Staff only, OVR-10)
    /// </summary>
    [HttpPatch("records/{recordId}/correction")]
    [HttpPut("records/{recordId}")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> CorrectAttendance(int recordId, [FromBody] CorrectAttendanceDto dto, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var result = await _attendanceService.CorrectAttendanceAsync(recordId, dto, userId, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// OVR-10 (UC-23 A1, BR-62): contact notes of one attendance record; school staff with access to the class only, never Parents
    /// </summary>
    [HttpGet("records/{recordId}/contact-notes")]
    [Authorize(Roles = "Teacher,OfficeStaff,SchoolBoard")]
    public async Task<IActionResult> GetContactNotes(int recordId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _attendanceService.GetContactNotesAsync(recordId, userId, roles, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// OVR-10 (UC-23 A1): the homeroom Teacher or Office Staff record a contact note on an unexcused absence (MSG55; MSG09 / MSG01 if not allowed)
    /// </summary>
    [HttpPost("records/{recordId}/contact-notes")]
    [Authorize(Roles = "Teacher,OfficeStaff")]
    public async Task<IActionResult> AddContactNote(int recordId, [FromBody] CreateAbsenceContactNoteDto dto, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _attendanceService.AddContactNoteAsync(recordId, dto, userId, roles, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// OVR-10 (UC-23 A1): the note's author or Office Staff mark a wrong contact note superseded, with a reason (MSG319)
    /// </summary>
    [HttpPost("contact-notes/{noteId}/supersede")]
    [Authorize(Roles = "Teacher,OfficeStaff")]
    public async Task<IActionResult> SupersedeContactNote(int noteId, [FromBody] SupersedeAbsenceContactNoteDto dto, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _attendanceService.SupersedeContactNoteAsync(noteId, dto, userId, roles, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// OVR-10 (UC-23 A2): unexcused absences of a date range with no contact note in effect, optionally of one class
    /// </summary>
    [HttpGet("absences-without-contact-note")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> GetAbsencesWithoutContactNote(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? classId,
        CancellationToken ct)
    {
        var roles = User.GetRoles();
        var fromDate = from ?? _clock.Today.AddDays(-30);
        var toDate = to ?? _clock.Today;
        var result = await _attendanceService.GetAbsencesWithoutContactNoteAsync(fromDate, toDate, classId, roles, ct);
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
    /// API-5b: Ma trận điểm danh theo ngày của cả lớp (tối đa 31 ngày)
    /// </summary>
    [HttpGet("classes/{classId}/matrix")]
    [Authorize(Roles = "Teacher,OfficeStaff,SchoolBoard,Admin")]
    public async Task<IActionResult> GetClassMatrix(
        int classId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var fromDate = from ?? _clock.Today.AddDays(-30);
        var toDate = to ?? _clock.Today;
        var result = await _attendanceService.GetClassMatrixAsync(classId, fromDate, toDate, userId, roles, ct);
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

    /// <summary>
    /// Giáo viên gửi yêu cầu Phòng Giáo vụ hỗ trợ điểm danh / điều chỉnh lại kèm lý do
    /// </summary>
    [HttpPost("classes/{classId}/request-reattendance")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> RequestReattendance(
        int classId,
        [FromBody] RequestReattendanceDto dto,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var result = await _attendanceService.RequestReattendanceAsync(classId, dto, userId, ct);
        return ToActionResult(result);
    }
}
