using Core.DTOs;
using Core.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        var count = await _unitOfWork.Notifications.CountUnreadAsync(userId, cancellationToken);
        return new UnreadCountDto { Count = count };
    }

    public async Task<PaginatedResult<NotificationDto>> GetNotificationsAsync(int userId, int page, int size, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 100) size = 20;

        var paged = await _unitOfWork.Notifications.GetPagedAsync(userId, page, size, cancellationToken);
        return new PaginatedResult<NotificationDto>
        {
            Items = paged.Items.Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                IsRead = n.IsRead,
                LinkUrl = n.LinkUrl,
                Type = n.Type,
                ReferenceKey = n.ReferenceKey,
                CreatedAt = n.CreatedAt
            }).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex = paged.PageIndex,
            PageSize = paged.PageSize
        };
    }

    public async Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null || notification.UserId != userId)
        {
            return false;
        }

        notification.IsRead = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Notifications.MarkAllReadAsync(userId, cancellationToken);
    }
}
