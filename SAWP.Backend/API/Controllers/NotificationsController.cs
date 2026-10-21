using API.Extensions;
using Core.DTOs;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// API-8a: Đếm số thông báo chưa đọc (dành cho Polling 60s trên FE)
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var count = await _unitOfWork.Notifications.CountUnreadAsync(userId, ct);
        return Ok(ApiResponse<UnreadCountDto>.Ok(new UnreadCountDto { Count = count }));
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
        if (page < 1) page = 1;
        if (size < 1 || size > 100) size = 20;

        var paged = await _unitOfWork.Notifications.GetPagedAsync(userId, page, size, ct);
        var dtos = paged.Items.Select(n => new NotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Content = n.Content,
            IsRead = n.IsRead,
            LinkUrl = n.LinkUrl,
            Type = n.Type,
            ReferenceKey = n.ReferenceKey,
            CreatedAt = n.CreatedAt
        }).ToList();

        var result = new PaginatedResult<NotificationDto>
        {
            Items = dtos,
            TotalCount = paged.TotalCount,
            PageIndex = paged.PageIndex,
            PageSize = paged.PageSize
        };

        return Ok(ApiResponse<PaginatedResult<NotificationDto>>.Ok(result));
    }

    /// <summary>
    /// API-8c: Đánh dấu một thông báo là đã đọc
    /// </summary>
    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var notif = await _unitOfWork.Notifications.GetByIdAsync(id, ct);
        if (notif == null || notif.UserId != userId)
        {
            return NotFound(ApiResponse<bool>.Fail("Không tìm thấy thông báo."));
        }

        notif.IsRead = true;
        await _unitOfWork.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>
    /// API-8d: Đánh dấu tất cả thông báo là đã đọc
    /// </summary>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _unitOfWork.Notifications.MarkAllReadAsync(userId, ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
