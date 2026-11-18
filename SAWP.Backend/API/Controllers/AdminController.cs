using API.Extensions;
using Core.Constants;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : BaseApiController
{
    private readonly IAdminService _adminService;
    private readonly IStaffImporter _staffImporter;

    public AdminController(IAdminService adminService, IStaffImporter staffImporter)
    {
        _adminService = adminService;
        _staffImporter = staffImporter;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _adminService.GetUsersPagedAsync(pageIndex, pageSize, role, isActive, search, ct);
        return ToActionResult(result);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken ct = default)
    {
        var result = await _adminService.GetRolesAsync(ct);
        return ToActionResult(result);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.CreateUserAsync(userId.Value, request, ct);
        return ToActionResult(result);
    }

    [HttpPut("users/{id}/roles")]
    public async Task<IActionResult> UpdateUserRoles(int id, [FromBody] UpdateUserRolesRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpdateUserRolesAsync(userId.Value, id, request, ct);
        return ToActionResult(result);
    }

    [HttpPatch("users/{id}/status")]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpdateUserStatusAsync(userId.Value, id, request.IsActive, ct);
        return ToActionResult(result);
    }

    [HttpPost("users/{id}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(int id, [FromBody] ResetUserPasswordRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.ResetUserPasswordAsync(userId.Value, id, request.NewPassword, ct);
        return ToActionResult(result);
    }

    [HttpPost("staff/import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ImportStaff(IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<int>.Fail(SystemMessages.MSG41));
        }

        await using var stream = file.OpenReadStream();
        var result = await _staffImporter.ImportStaffAsync(stream, ct);
        return ToActionResult(result);
    }

    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendarDays([FromQuery] int? schoolYearId = null, [FromQuery] string? month = null, CancellationToken ct = default)
    {
        var result = await _adminService.GetCalendarDaysAsync(schoolYearId, month, ct);
        return ToActionResult(result);
    }

    [HttpPut("calendar/day")]
    public async Task<IActionResult> UpsertCalendarDay([FromBody] UpdateCalendarDayDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpsertCalendarDayAsync(userId.Value, request, ct);
        return ToActionResult(result);
    }

    [HttpGet("lunch-cutoff")]
    public async Task<IActionResult> GetLunchServiceDays([FromQuery] string? month = null, CancellationToken ct = default)
    {
        var result = await _adminService.GetLunchServiceDaysAsync(month, ct);
        return ToActionResult(result);
    }

    [HttpPut("lunch-cutoff")]
    public async Task<IActionResult> UpsertLunchCutoff([FromBody] UpdateLunchCutoffDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpsertLunchCutoffAsync(userId.Value, request, ct);
        return ToActionResult(result);
    }
}
