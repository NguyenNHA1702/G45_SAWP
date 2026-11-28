using Core.DTOs;

namespace Services.Interfaces;

public interface INotificationService
{
    Task<UnreadCountDto> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    Task<PaginatedResult<NotificationDto>> GetNotificationsAsync(int userId, int page, int size, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu một thông báo là đã đọc; trả về false nếu không tìm thấy hoặc không thuộc về người dùng
    /// </summary>
    Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);

    Task<ApiResponse<BroadcastNotificationResultDto>> BroadcastAsync(
        int senderUserId,
        BroadcastNotificationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<BroadcastHistoryItemDto>>> GetBroadcastHistoryAsync(
        int limit = 25,
        CancellationToken cancellationToken = default);
}
