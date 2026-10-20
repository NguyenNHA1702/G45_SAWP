using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Extensions;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyNotifications(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<List<NotificationDto>>.Fail("Vui lòng đăng nhập."));
        }

        var list = await _unitOfWork.Repository<Notification>()
            .Query()
            .Where(n => n.UserId == userId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                IsRead = n.IsRead,
                LinkUrl = n.LinkUrl,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<NotificationDto>>.Ok(list));
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        var notif = await _unitOfWork.Repository<Notification>()
            .Query()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (notif != null)
        {
            notif.IsRead = true;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Ok(ApiResponse<bool>.Ok(true));
    }
}

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string? LinkUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
