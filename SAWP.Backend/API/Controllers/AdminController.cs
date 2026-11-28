using API.Extensions;
using Core.Constants;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    [Authorize(Roles = "Admin")]
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

    [HttpGet("staff")]
    [Authorize(Roles = "Admin,SchoolBoard")]
    public async Task<IActionResult> GetStaff(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _adminService.GetStaffPagedAsync(pageIndex, pageSize, role, isActive, search, ct);
        return ToActionResult(result);
    }

    [HttpGet("roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetRoles(CancellationToken ct = default)
    {
        var result = await _adminService.GetRolesAsync(ct);
        return ToActionResult(result);
    }

    [HttpPost("users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.CreateUserAsync(userId.Value, request, ct);
        return ToActionResult(result);
    }

    [HttpPut("users/{id}/roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUserRoles(int id, [FromBody] UpdateUserRolesRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpdateUserRolesAsync(userId.Value, id, request, ct);
        return ToActionResult(result);
    }

    [HttpPatch("users/{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpdateUserStatusAsync(userId.Value, id, request.IsActive, ct);
        return ToActionResult(result);
    }

    [HttpDelete("users/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.DeleteUserAsync(userId.Value, id, ct);
        return ToActionResult(result);
    }

    [HttpPost("users/{id:int}/resend-activation")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResendActivationLink(int id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.ResendActivationLinkAsync(userId.Value, id, ct);
        return ToActionResult(result);
    }

    [HttpPost("users/{id:int}/change-email")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeUserEmail(int id, [FromBody] AdminChangeEmailRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.ChangeUserEmailAsync(userId.Value, id, request.NewEmail, ct);
        return ToActionResult(result);
    }

    [HttpPost("users/{id}/reset-password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetUserPassword(int id, [FromBody] ResetUserPasswordRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.ResetUserPasswordAsync(userId.Value, id, request.NewPassword, ct);
        return ToActionResult(result);
    }

    [HttpPost("staff/import")]
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin,OfficeStaff,Parent,Teacher,ServiceStaff,SchoolBoard")]
    public async Task<IActionResult> GetCalendarDays([FromQuery] int? schoolYearId = null, [FromQuery] string? month = null, CancellationToken ct = default)
    {
        var result = await _adminService.GetCalendarDaysAsync(schoolYearId, month, ct);
        return ToActionResult(result);
    }

    [HttpPut("calendar/day")]
    [Authorize(Roles = "Admin,OfficeStaff")]
    public async Task<IActionResult> UpsertCalendarDay([FromBody] UpdateCalendarDayDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpsertCalendarDayAsync(userId.Value, request, ct);
        return ToActionResult(result);
    }

    [HttpGet("lunch-cutoff")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetLunchServiceDays([FromQuery] string? month = null, CancellationToken ct = default)
    {
        var result = await _adminService.GetLunchServiceDaysAsync(month, ct);
        return ToActionResult(result);
    }

    [HttpPut("lunch-cutoff")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpsertLunchCutoff([FromBody] UpdateLunchCutoffDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpsertLunchCutoffAsync(userId.Value, request, ct);
        return ToActionResult(result);
    }

    // ---- UC-54: Phân quyền chi tiết theo vai trò ----

    [HttpGet("permissions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPermissionsMatrix(CancellationToken ct = default)
    {
        var result = await _adminService.GetRolePermissionsMatrixAsync(ct);
        return ToActionResult(result);
    }

    [HttpPut("roles/{roleId:int}/permissions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRolePermissions(int roleId, [FromBody] UpdateRolePermissionsDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpdateRolePermissionsAsync(userId.Value, roleId, request, ct);
        return ToActionResult(result);
    }

    // ---- UC-55: Liên kết thủ công User <-> StaffProfile / Guardian ----

    [HttpGet("users/{id:int}/profile-links")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserProfileLinks(int id, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await _adminService.GetUserProfileLinksAsync(id, search, ct);
        return ToActionResult(result);
    }

    [HttpPut("users/{id:int}/profile-link")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUserProfileLink(int id, [FromBody] UpdateUserProfileLinkDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpdateUserProfileLinkAsync(userId.Value, id, request, ct);
        return ToActionResult(result);
    }

    // ---- UC-56: Danh mục tham chiếu hệ thống ----

    [HttpGet("reference-items")]
    [Authorize(Roles = "Admin,OfficeStaff,Teacher,ServiceStaff,SchoolBoard,Parent")]
    public async Task<IActionResult> GetReferenceItems([FromQuery] string? category = null, [FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        var result = await _adminService.GetReferenceItemsAsync(category, activeOnly, ct);
        return ToActionResult(result);
    }

    [HttpPost("reference-items")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateReferenceItem([FromBody] UpsertReferenceItemDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpsertReferenceItemAsync(userId.Value, null, request, ct);
        return ToActionResult(result);
    }

    [HttpPut("reference-items/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateReferenceItem(int id, [FromBody] UpsertReferenceItemDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpsertReferenceItemAsync(userId.Value, id, request, ct);
        return ToActionResult(result);
    }

    // ---- UC-57: Mẫu thông báo hệ thống ----

    [HttpGet("notification-templates")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetNotificationTemplates(CancellationToken ct = default)
    {
        var result = await _adminService.GetNotificationTemplatesAsync(ct);
        return ToActionResult(result);
    }

    [HttpPut("notification-templates/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateNotificationTemplate(int id, [FromBody] UpdateNotificationTemplateDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _adminService.UpdateNotificationTemplateAsync(userId.Value, id, request, ct);
        return ToActionResult(result);
    }
}
