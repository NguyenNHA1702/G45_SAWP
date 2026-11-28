using Core.Common;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService? _auditService;

    public NotificationService(IUnitOfWork unitOfWork, IAuditService? auditService = null)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
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
                CreatedAt = UtcDateTimeHelper.Normalize(n.CreatedAt)
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

    public async Task<ApiResponse<BroadcastNotificationResultDto>> BroadcastAsync(
        int senderUserId,
        BroadcastNotificationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        var content = request.Content?.Trim() ?? string.Empty;
        if (title.Length < 5 || title.Length > 150)
        {
            return ApiResponse<BroadcastNotificationResultDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Tiêu đề thông báo phải từ 5 đến 150 ký tự." });
        }

        if (content.Length < 5 || content.Length > 450)
        {
            return ApiResponse<BroadcastNotificationResultDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Nội dung thông báo phải từ 5 đến 450 ký tự." });
        }

        var scope = string.IsNullOrWhiteSpace(request.TargetScope) ? "All" : request.TargetScope.Trim();
        var userIds = new HashSet<int>();

        if (string.Equals(scope, "All", StringComparison.OrdinalIgnoreCase))
        {
            var ids = await _unitOfWork.Repository<User>()
                .Query()
                .AsNoTracking()
                .Where(u => u.Status == AccountStatus.Active)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
            userIds.UnionWith(ids);
        }
        else if (string.Equals(scope, "Role", StringComparison.OrdinalIgnoreCase))
        {
            var roleName = request.TargetRole?.Trim();
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return ApiResponse<BroadcastNotificationResultDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Vui lòng chọn vai trò nhận thông báo." });
            }

            var ids = await _unitOfWork.Repository<User>()
                .Query()
                .AsNoTracking()
                .Where(u => u.Status == AccountStatus.Active
                         && u.UserRoles.Any(ur => ur.Role.Name == roleName))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
            userIds.UnionWith(ids);
        }
        else if (string.Equals(scope, "Grade", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.Grade.HasValue || request.Grade.Value < 1 || request.Grade.Value > 5)
            {
                return ApiResponse<BroadcastNotificationResultDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Khối lớp phải từ 1 đến 5." });
            }

            var classIds = await _unitOfWork.Repository<Class>()
                .Query()
                .AsNoTracking()
                .Where(c => c.Grade == request.Grade.Value && c.Status == "Active" && c.SchoolYear.IsActive)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            await CollectClassRecipientsAsync(classIds, userIds, cancellationToken);
        }
        else if (string.Equals(scope, "Class", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.ClassId.HasValue || request.ClassId.Value <= 0)
            {
                return ApiResponse<BroadcastNotificationResultDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Vui lòng chọn lớp nhận thông báo." });
            }

            await CollectClassRecipientsAsync(new[] { request.ClassId.Value }, userIds, cancellationToken);
        }
        else
        {
            return ApiResponse<BroadcastNotificationResultDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Phạm vi gửi thông báo không hợp lệ (All, Role, Grade, Class)." });
        }

        if (userIds.Count == 0)
        {
            return ApiResponse<BroadcastNotificationResultDto>.Fail(
                SystemMessages.MSG43,
                new List<string> { "Không tìm thấy tài khoản người nhận phù hợp đang hoạt động." });
        }

        var now = DateTime.UtcNow;
        var refKey = $"BROADCAST:{Guid.NewGuid():N}";
        var linkUrl = string.IsNullOrWhiteSpace(request.LinkUrl) ? "/dashboard" : request.LinkUrl.Trim();
        var notifRepo = _unitOfWork.Repository<Notification>();

        foreach (var uid in userIds)
        {
            await notifRepo.AddAsync(new Notification
            {
                UserId = uid,
                Title = title,
                Content = content,
                Type = "SchoolBroadcast",
                ReferenceKey = refKey,
                LinkUrl = linkUrl,
                CreatedAt = now,
                IsRead = false
            }, cancellationToken);
        }

        if (_auditService != null)
        {
            await _auditService.LogActionAsync(
                senderUserId,
                AuditActions.SchoolBroadcastSend,
                nameof(Notification),
                refKey,
                null,
                new { TargetScope = scope, request.TargetRole, request.Grade, request.ClassId, RecipientCount = userIds.Count, Title = title },
                null,
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<BroadcastNotificationResultDto>.Ok(new BroadcastNotificationResultDto
        {
            RecipientCount = userIds.Count,
            ReferenceKey = refKey,
            SentAt = now
        }, $"Đã gửi thông báo tới {userIds.Count} người nhận.");
    }

    public async Task<ApiResponse<List<BroadcastHistoryItemDto>>> GetBroadcastHistoryAsync(
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 100);
        var items = await _unitOfWork.Repository<Notification>()
            .Query()
            .AsNoTracking()
            .Where(n => n.Type == "SchoolBroadcast" && n.ReferenceKey != null)
            .GroupBy(n => n.ReferenceKey!)
            .Select(g => new BroadcastHistoryItemDto
            {
                ReferenceKey = g.Key,
                Title = g.Select(x => x.Title).FirstOrDefault() ?? string.Empty,
                Content = g.Select(x => x.Content).FirstOrDefault() ?? string.Empty,
                RecipientCount = g.Count(),
                ReadCount = g.Count(x => x.IsRead),
                SentAt = g.Max(x => x.CreatedAt)
            })
            .OrderByDescending(x => x.SentAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.SentAt = UtcDateTimeHelper.Normalize(item.SentAt);
        }

        return ApiResponse<List<BroadcastHistoryItemDto>>.Ok(items);
    }

    private async Task CollectClassRecipientsAsync(IEnumerable<int> classIds, HashSet<int> userIds, CancellationToken ct)
    {
        var ids = classIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var teacherUserIds = await _unitOfWork.Repository<Class>()
            .Query()
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id) && c.HomeroomTeacher != null && c.HomeroomTeacher.User.Status == AccountStatus.Active)
            .Select(c => c.HomeroomTeacher!.UserId)
            .ToListAsync(ct);
        userIds.UnionWith(teacherUserIds);

        var studentIds = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .AsNoTracking()
            .Where(ca => ids.Contains(ca.ClassId) && ca.IsActive)
            .Select(ca => ca.StudentId)
            .Distinct()
            .ToListAsync(ct);

        if (studentIds.Count > 0)
        {
            var parentUserIds = await _unitOfWork.Repository<StudentGuardian>()
                .Query()
                .AsNoTracking()
                .Where(sg => studentIds.Contains(sg.StudentId)
                          && sg.Guardian.UserId != null
                          && sg.Guardian.User!.Status == AccountStatus.Active)
                .Select(sg => sg.Guardian.UserId!.Value)
                .ToListAsync(ct);
            userIds.UnionWith(parentUserIds);
        }
    }
}
