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
}
