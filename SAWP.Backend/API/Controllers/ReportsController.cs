using API.Extensions;
using Core.DTOs;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

/// <summary>
/// GAP-04 (UC-49..UC-52): Standardized reporting endpoints for Students/Classes, Attendance, Lunch, and Requests.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : BaseApiController
{
    private readonly IClassService _classService;
    private readonly IStudentService _studentService;
    private readonly IAttendanceService _attendanceService;
    private readonly ILunchService _lunchService;
    private readonly IRequestWorkflowEngine _workflowEngine;
    private readonly ISchoolClock _clock;

    public ReportsController(
        IClassService classService,
        IStudentService studentService,
        IAttendanceService attendanceService,
        ILunchService lunchService,
        IRequestWorkflowEngine workflowEngine,
        ISchoolClock clock)
    {
        _classService = classService;
        _studentService = studentService;
        _attendanceService = attendanceService;
        _lunchService = lunchService;
        _workflowEngine = workflowEngine;
        _clock = clock;
    }

    /// <summary>UC-49: Student & Class overview report for OfficeStaff / SchoolBoard.</summary>
    [HttpGet("students")]
    [Authorize(Roles = "OfficeStaff,SchoolBoard")]
    public async Task<IActionResult> GetStudentAndClassReport([FromQuery] int? schoolYearId, CancellationToken ct)
    {
        var classesRes = await _classService.GetClassesAsync(schoolYearId, null, null, includeDraft: false, ct);
        if (!classesRes.Success) return ToActionResult(classesRes);

        var activeRes = await _studentService.GetStudentsPagedAsync(new StudentFilterDto { Status = "Active", SchoolYearId = schoolYearId, PageIndex = 1, PageSize = 1 }, ct);
        var inactiveRes = await _studentService.GetStudentsPagedAsync(new StudentFilterDto { Status = "Inactive", SchoolYearId = schoolYearId, PageIndex = 1, PageSize = 1 }, ct);
        var archivedRes = await _studentService.GetStudentsPagedAsync(new StudentFilterDto { Status = "Archived", SchoolYearId = schoolYearId, PageIndex = 1, PageSize = 1 }, ct);

        var classes = classesRes.Data ?? new List<ClassSummaryDto>();
        return Ok(ApiResponse<object>.Ok(new
        {
            TotalClasses = classes.Count,
            TotalCapacity = classes.Sum(c => c.Capacity),
            TotalEnrolledInClasses = classes.Sum(c => c.EnrolledCount),
            ActiveStudents = activeRes.Data?.TotalCount ?? 0,
            InactiveStudents = inactiveRes.Data?.TotalCount ?? 0,
            ArchivedStudents = archivedRes.Data?.TotalCount ?? 0,
            Classes = classes
        }));
    }

    /// <summary>UC-50: Attendance summary report for a class over a date range.</summary>
    [HttpGet("attendance/summary")]
    [Authorize(Roles = "OfficeStaff,SchoolBoard,Teacher")]
    public async Task<IActionResult> GetAttendanceSummary(
        [FromQuery] int classId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var start = (from ?? _clock.Today.AddDays(-30)).Date;
        var end = (to ?? _clock.Today).Date;
        var res = await _attendanceService.GetClassSummaryAsync(classId, start, end, User.GetRequiredUserId(), User.GetRoles(), ct);
        return ToActionResult(res);
    }

    /// <summary>UC-50: Attendance matrix report for a class over up to 31 days.</summary>
    [HttpGet("attendance/matrix")]
    [Authorize(Roles = "OfficeStaff,SchoolBoard,Teacher")]
    public async Task<IActionResult> GetAttendanceMatrix(
        [FromQuery] int classId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var start = (from ?? _clock.Today.AddDays(-6)).Date;
        var end = (to ?? _clock.Today).Date;
        var res = await _attendanceService.GetClassMatrixAsync(classId, start, end, User.GetRequiredUserId(), User.GetRoles(), ct);
        return ToActionResult(res);
    }

    /// <summary>UC-51: Boarding lunch period report.</summary>
    [HttpGet("lunch/period")]
    [Authorize(Roles = "ServiceStaff,OfficeStaff,SchoolBoard,Teacher")]
    public async Task<IActionResult> GetLunchPeriodReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? classId,
        CancellationToken ct)
    {
        var start = (from ?? new DateTime(_clock.Today.Year, _clock.Today.Month, 1)).Date;
        var end = (to ?? start.AddMonths(1).AddDays(-1)).Date;
        var res = await _lunchService.GetPeriodReportAsync(start, end, classId, User.GetRequiredUserId(), User.GetRoles(), ct);
        return ToActionResult(res);
    }

    /// <summary>UC-51: Daily boarding headcount report.</summary>
    [HttpGet("lunch/daily")]
    [Authorize(Roles = "ServiceStaff,OfficeStaff,SchoolBoard,Teacher")]
    public async Task<IActionResult> GetLunchDailyReport([FromQuery] DateTime? date, CancellationToken ct)
    {
        var targetDate = (date ?? _clock.Today).Date;
        var res = await _lunchService.GetMealHeadcountAsync(targetDate, User.GetRequiredUserId(), User.GetRoles(), ct);
        return ToActionResult(res);
    }

    /// <summary>UC-52: Administrative requests statistics report.</summary>
    [HttpGet("requests")]
    [Authorize(Roles = "OfficeStaff,SchoolBoard")]
    public async Task<IActionResult> GetRequestsReport(CancellationToken ct)
    {
        var res = await _workflowEngine.GetRequestStatisticsAsync(ct);
        return ToActionResult(res);
    }
}
