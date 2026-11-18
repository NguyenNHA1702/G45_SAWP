using Core.DTOs;

namespace Services.Interfaces;

public interface IAdminService
{
    Task<ApiResponse<PaginatedResult<AdminUserDto>>> GetUsersPagedAsync(
        int pageIndex, int pageSize, string? role, bool? isActive, string? search, CancellationToken ct = default);
    Task<ApiResponse<List<RoleOptionDto>>> GetRolesAsync(CancellationToken ct = default);
    Task<ApiResponse<AdminUserDto>> CreateUserAsync(int adminUserId, CreateUserRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<AdminUserDto>> UpdateUserRolesAsync(int adminUserId, int targetUserId, UpdateUserRolesRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateUserStatusAsync(int adminUserId, int targetUserId, bool isActive, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResetUserPasswordAsync(int adminUserId, int targetUserId, string newPassword, CancellationToken ct = default);

    Task<ApiResponse<List<SchoolCalendarDayDto>>> GetCalendarDaysAsync(int? schoolYearId, string? month, CancellationToken ct = default);
    Task<ApiResponse<SchoolCalendarDayDto>> UpsertCalendarDayAsync(int adminUserId, UpdateCalendarDayDto request, CancellationToken ct = default);

    Task<ApiResponse<List<LunchCutoffDayDto>>> GetLunchServiceDaysAsync(string? month, CancellationToken ct = default);
    Task<ApiResponse<LunchCutoffDayDto>> UpsertLunchCutoffAsync(int adminUserId, UpdateLunchCutoffDto request, CancellationToken ct = default);
}
