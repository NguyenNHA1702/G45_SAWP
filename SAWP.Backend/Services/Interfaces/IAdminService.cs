using Core.DTOs;

namespace Services.Interfaces;

public interface IAdminService
{
    Task<ApiResponse<PaginatedResult<AdminUserDto>>> GetUsersPagedAsync(
        int pageIndex, int pageSize, string? role, bool? isActive, string? search, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResult<AdminUserDto>>> GetStaffPagedAsync(
        int pageIndex, int pageSize, string? role, bool? isActive, string? search, CancellationToken ct = default);
    Task<ApiResponse<List<RoleOptionDto>>> GetRolesAsync(CancellationToken ct = default);
    Task<ApiResponse<AdminUserDto>> CreateUserAsync(int adminUserId, CreateUserRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<AdminUserDto>> UpdateUserRolesAsync(int adminUserId, int targetUserId, UpdateUserRolesRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateUserStatusAsync(int adminUserId, int targetUserId, bool isActive, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteUserAsync(int adminUserId, int targetUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResendActivationLinkAsync(int adminUserId, int targetUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ChangeUserEmailAsync(int adminUserId, int targetUserId, string newEmail, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResetUserPasswordAsync(int adminUserId, int targetUserId, string newPassword, CancellationToken ct = default);

    Task<ApiResponse<List<SchoolCalendarDayDto>>> GetCalendarDaysAsync(int? schoolYearId, string? month, CancellationToken ct = default);
    Task<ApiResponse<SchoolCalendarDayDto>> UpsertCalendarDayAsync(int adminUserId, UpdateCalendarDayDto request, CancellationToken ct = default);

    Task<ApiResponse<List<LunchCutoffDayDto>>> GetLunchServiceDaysAsync(string? month, CancellationToken ct = default);
    Task<ApiResponse<LunchCutoffDayDto>> UpsertLunchCutoffAsync(int adminUserId, UpdateLunchCutoffDto request, CancellationToken ct = default);

    // GAP-05: UC-54..UC-57
    Task<ApiResponse<RolePermissionMatrixDto>> GetRolePermissionsMatrixAsync(CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateRolePermissionsAsync(int adminUserId, int roleId, UpdateRolePermissionsDto request, CancellationToken ct = default);
    Task<ApiResponse<AdminProfileLinkingOverviewDto>> GetUserProfileLinksAsync(int targetUserId, string? search = null, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateUserProfileLinkAsync(int adminUserId, int targetUserId, UpdateUserProfileLinkDto request, CancellationToken ct = default);
    Task<ApiResponse<List<ReferenceItemDto>>> GetReferenceItemsAsync(string? category = null, bool activeOnly = false, CancellationToken ct = default);
    Task<ApiResponse<ReferenceItemDto>> UpsertReferenceItemAsync(int adminUserId, int? id, UpsertReferenceItemDto request, CancellationToken ct = default);
    Task<ApiResponse<List<NotificationTemplateDto>>> GetNotificationTemplatesAsync(CancellationToken ct = default);
    Task<ApiResponse<NotificationTemplateDto>> UpdateNotificationTemplateAsync(int adminUserId, int id, UpdateNotificationTemplateDto request, CancellationToken ct = default);
}
