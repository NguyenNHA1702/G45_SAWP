using Core.DTOs;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LunchController : ControllerBase
{
    private readonly ILunchService _lunchService;

    public LunchController(ILunchService lunchService)
    {
        _lunchService = lunchService;
    }

    /// <summary>
    /// Lấy bảng vận hành suất ăn bán trú thời gian thực (Live Kitchen Operations Board)
    /// </summary>
    [HttpGet("live-board")]
    public async Task<IActionResult> GetLiveBoard([FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateTime.Today;
        var result = await _lunchService.GetDailyMealBoardAsync(targetDate, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy báo cáo tổng số suất ăn theo ngày
    /// </summary>
    [HttpGet("daily-count")]
    public async Task<IActionResult> GetDailyCount([FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateTime.Today;
        var result = await _lunchService.GetDailyMealCountAsync(targetDate, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Chốt số lượng suất ăn gửi nhà bếp lúc 08h15
    /// </summary>
    [HttpPost("lock-day")]
    public async Task<IActionResult> LockDay([FromBody] LockMealDayRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _lunchService.LockDailyMealAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Khôi phục suất ăn khi học sinh có đơn nghỉ nhưng thực tế vẫn đi học
    /// </summary>
    [HttpPost("restore-meal")]
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

