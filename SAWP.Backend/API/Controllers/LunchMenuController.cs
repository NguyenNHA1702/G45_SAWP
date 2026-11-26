using API.Extensions;
using Core.DTOs;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/lunch")]
public class LunchMenuController : BaseApiController
{
    private readonly ILunchMenuService _lunchMenuService;

    public LunchMenuController(ILunchMenuService lunchMenuService)
    {
        _lunchMenuService = lunchMenuService;
    }

    // ==================== THƯ VIỆN MÓN ĂN ====================

    [HttpGet("dishes")]
    [Authorize]
    public async Task<IActionResult> GetDishes(
        [FromQuery] DishCategory? category,
        [FromQuery] MealType? mealType,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await _lunchMenuService.GetDishesAsync(category, mealType, search, includeInactive, ct);
        return ToActionResult(result);
    }

    [HttpPost("dishes")]
    [Authorize(Roles = "ServiceStaff,Admin")]
    public async Task<IActionResult> CreateDish([FromBody] UpsertDishRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var result = await _lunchMenuService.CreateDishAsync(userId, request, ct);
        return ToActionResult(result);
    }

    [HttpPut("dishes/{id}")]
    [Authorize(Roles = "ServiceStaff,Admin")]
    public async Task<IActionResult> UpdateDish(int id, [FromBody] UpsertDishRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var result = await _lunchMenuService.UpdateDishAsync(id, userId, request, ct);
        return ToActionResult(result);
    }

    [HttpDelete("dishes/{id}")]
    [Authorize(Roles = "ServiceStaff,Admin")]
    public async Task<IActionResult> DeleteDish(int id, CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var result = await _lunchMenuService.DeleteDishAsync(id, userId, ct);
        return ToActionResult(result);
    }

    // ==================== MẪU THỰC ĐƠN TUẦN ====================

    [HttpGet("menu-templates")]
    [Authorize(Roles = "ServiceStaff,OfficeStaff,SchoolBoard,Teacher,Admin")]
    public async Task<IActionResult> GetTemplates([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var result = await _lunchMenuService.GetTemplatesAsync(includeInactive, ct);
        return ToActionResult(result);
    }

    [HttpPost("menu-templates")]
    [Authorize(Roles = "ServiceStaff,Admin")]
    public async Task<IActionResult> CreateTemplate([FromBody] UpsertWeeklyMenuTemplateRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var result = await _lunchMenuService.CreateTemplateAsync(userId, request, ct);
        return ToActionResult(result);
    }

    [HttpPut("menu-templates/{id}")]
    [Authorize(Roles = "ServiceStaff,Admin")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] UpsertWeeklyMenuTemplateRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var result = await _lunchMenuService.UpdateTemplateAsync(id, userId, request, ct);
        return ToActionResult(result);
    }

    [HttpDelete("menu-templates/{id}")]
    [Authorize(Roles = "ServiceStaff,Admin")]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var result = await _lunchMenuService.DeleteTemplateAsync(id, userId, ct);
        return ToActionResult(result);
    }

    [HttpPost("menu-templates/{id}/apply")]
    [Authorize(Roles = "ServiceStaff,Admin")]
    public async Task<IActionResult> ApplyTemplate(int id, [FromBody] ApplyMenuTemplateRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var result = await _lunchMenuService.ApplyTemplateAsync(id, userId, request, ct);
        return ToActionResult(result);
    }

    // ==================== HỒ SƠ CHẾ ĐỘ ĂN HỌC SINH ====================

    [HttpGet("students/{studentId}/meal-profile")]
    [Authorize(Roles = "Parent,Teacher,ServiceStaff,OfficeStaff,SchoolBoard,Admin")]
    public async Task<IActionResult> GetStudentMealProfile(int studentId, CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _lunchMenuService.GetStudentMealProfileAsync(studentId, userId, roles, ct);
        return ToActionResult(result);
    }

    [HttpPut("students/{studentId}/meal-profile")]
    [Authorize(Roles = "Parent,ServiceStaff,Admin")]
    public async Task<IActionResult> UpsertStudentMealProfile(int studentId, [FromBody] UpsertStudentMealProfileRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var roles = User.GetRoles();
        var result = await _lunchMenuService.UpsertStudentMealProfileAsync(studentId, userId, roles, request, ct);
        return ToActionResult(result);
    }
}
