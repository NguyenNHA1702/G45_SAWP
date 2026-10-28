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
public class LunchController : BaseApiController
{
    private readonly ILunchService _lunchService;
    private readonly ISchoolClock _clock;

    public LunchController(ILunchService lunchService, ISchoolClock clock)
    {
        _lunchService = lunchService;
        _clock = clock;
    }

    /// <summary>
    /// UC-32: Lấy cấu hình ngày phục vụ, hạn đăng ký/hủy và thực đơn/ghi chú trong khoảng ngày
    /// </summary>
    [HttpGet("service-days")]
    [Authorize]
    public async Task<IActionResult> GetServiceDays([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var start = (from ?? _clock.Today).Date;
        var end = (to ?? start.AddDays(30)).Date;
        var result = await _lunchService.GetServiceDaysAsync(start, end, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// UC-32: Nhân viên bán trú cấu hình ngày phục vụ, hạn đăng ký/hủy (08h15) và ghi chú/thực đơn
    /// </summary>
    [HttpPut("service-days")]
    [Authorize(Roles = "ServiceStaff,Admin")]
    public async Task<IActionResult> UpsertServiceDays([FromBody] UpsertLunchServiceDaysRequestDto request, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var result = await _lunchService.UpsertServiceDaysAsync(userId, request, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// UC-31: Phụ huynh xem lịch bán trú và trạng thái đăng ký của con theo tháng
    /// </summary>
    [HttpGet("student-schedule")]
    [Authorize]
    public async Task<IActionResult> GetStudentSchedule([FromQuery] int studentId, [FromQuery] string? month, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var targetMonth = string.IsNullOrWhiteSpace(month) ? _clock.Today.ToString("yyyy-MM") : month;
        var result = await _lunchService.GetStudentScheduleAsync(studentId, targetMonth, userId, roles, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// UC-29: Phụ huynh chọn một hoặc nhiều ngày còn mở và xác nhận đăng ký độc lập cho từng ngày
    /// </summary>
    [HttpPost("register-batch")]
    [Authorize(Roles = "Parent,ServiceStaff,Admin")]
    public async Task<IActionResult> RegisterBatch([FromBody] BatchLunchRegistrationRequestDto request, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _lunchService.RegisterLunchBatchAsync(userId, roles, request, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// UC-30: Phụ huynh chọn đăng ký còn hiệu lực và hủy trước hạn hủy (08h15)
    /// </summary>
    [HttpPost("cancel")]
    [Authorize(Roles = "Parent,ServiceStaff,Admin")]
    public async Task<IActionResult> CancelRegistration([FromBody] CancelLunchRegistrationRequestDto request, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _lunchService.CancelRegistrationByParentAsync(userId, roles, request, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// API-7: Số liệu suất ăn phục vụ trong ngày cho tổ Bếp / Ban Giám hiệu
    /// </summary>
    [HttpGet("headcount")]
    [Authorize(Roles = "ServiceStaff,OfficeStaff,SchoolBoard,Admin")]
    public async Task<IActionResult> GetMealHeadcount([FromQuery] DateTime? date, CancellationToken ct)
    {
        var targetDate = date ?? _clock.Today;
        var result = await _lunchService.GetMealHeadcountAsync(targetDate, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// UC-33: Lấy bảng vận hành & báo cáo suất ăn bán trú theo ngày (hỗ trợ phân quyền theo role và lọc lớp)
    /// </summary>
    [HttpGet("live-board")]
    [Authorize(Roles = "ServiceStaff,OfficeStaff,SchoolBoard,Admin")]
    public async Task<IActionResult> GetLiveBoard([FromQuery] DateTime? date, [FromQuery] int? classId, CancellationToken cancellationToken)
    {
        var targetDate = date ?? _clock.Today;
        int? userId = User?.Identity?.IsAuthenticated == true ? User.GetRequiredUserId() : null;
        IReadOnlyList<UserRoles>? roles = User?.Identity?.IsAuthenticated == true ? User.GetRoles() : null;
        var result = await _lunchService.GetDailyMealBoardAsync(targetDate, userId, roles, classId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy báo cáo tổng số suất ăn theo ngày
    /// </summary>
    [HttpGet("daily-count")]
    public async Task<IActionResult> GetDailyCount([FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        var targetDate = date ?? _clock.Today;
        var result = await _lunchService.GetDailyMealCountAsync(targetDate, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Chốt số lượng suất ăn gửi nhà bếp (giờ chốt: CutoffTime của ngày hoặc School:LunchCutoffTime = 08:15)
    /// </summary>
    [HttpPost("lock-day")]
    [Authorize(Roles = "ServiceStaff,Admin")]
    public async Task<IActionResult> LockDay([FromBody] LockMealDayRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _lunchService.LockDailyMealAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Khôi phục suất ăn khi học sinh có đơn nghỉ nhưng thực tế vẫn đi học
    /// </summary>
    [HttpPost("restore-meal")]
    [Authorize(Roles = "ServiceStaff,Teacher,OfficeStaff,Admin")]
    public async Task<IActionResult> RestoreMeal([FromBody] RestoreMealRequest request, CancellationToken cancellationToken)
    {
        var result = await _lunchService.RestoreMealForStudentAsync(request.StudentId, request.Date, request.Reason, cancellationToken);
        return Ok(result);
    }
}

public class RestoreMealRequest
{
    public int StudentId { get; set; }
    public DateTime Date { get; set; }
    public string Reason { get; set; } = string.Empty;
}
