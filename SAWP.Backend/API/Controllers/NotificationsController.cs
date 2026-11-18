using API.Extensions;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// API-8a: Đếm số thông báo chưa đọc (dành cho Polling 60s trên FE)
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var result = await _notificationService.GetUnreadCountAsync(userId, ct);
        return Ok(ApiResponse<UnreadCountDto>.Ok(result));
    }

    /// <summary>
    /// API-8b: Lấy danh sách thông báo phân trang của người dùng hiện tại
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var result = await _notificationService.GetNotificationsAsync(userId, page, size, ct);
        return Ok(ApiResponse<PaginatedResult<NotificationDto>>.Ok(result));
    }

    /// <summary>
    /// API-8c: Đánh dấu một thông báo là đã đọc
    /// </summary>
    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        if (!await _notificationService.MarkAsReadAsync(userId, id, ct))
        {
            return NotFound(ApiResponse<bool>.Fail("Không tìm thấy thông báo."));
        }

        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>
    /// API-8d: Đánh dấu tất cả thông báo là đã đọc
    /// </summary>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _notificationService.MarkAllReadAsync(userId, ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
