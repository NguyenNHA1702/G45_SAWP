using Core.DTOs;
using Core.Enums;

namespace Services.Interfaces;

public interface ILunchMenuService
{
    // Thư viện món ăn
    Task<ApiResponse<List<DishDto>>> GetDishesAsync(
        DishCategory? category = null,
        MealType? mealType = null,
        string? search = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<DishDto>> CreateDishAsync(int userId, UpsertDishRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<DishDto>> UpdateDishAsync(int dishId, int userId, UpsertDishRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteDishAsync(int dishId, int userId, CancellationToken cancellationToken = default);

    // Mẫu thực đơn tuần
    Task<ApiResponse<List<WeeklyMenuTemplateDto>>> GetTemplatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ApiResponse<WeeklyMenuTemplateDto>> CreateTemplateAsync(int userId, UpsertWeeklyMenuTemplateRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WeeklyMenuTemplateDto>> UpdateTemplateAsync(int templateId, int userId, UpsertWeeklyMenuTemplateRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteTemplateAsync(int templateId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ApplyMenuTemplateResultDto>> ApplyTemplateAsync(int templateId, int userId, ApplyMenuTemplateRequestDto request, CancellationToken cancellationToken = default);

    // Hồ sơ chế độ ăn & dị ứng mặc định của học sinh
    Task<ApiResponse<StudentMealProfileDto>> GetStudentMealProfileAsync(
        int studentId,
        int userId,
        IReadOnlyList<UserRoles> roles,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StudentMealProfileDto>> UpsertStudentMealProfileAsync(
        int studentId,
        int userId,
        IReadOnlyList<UserRoles> roles,
        UpsertStudentMealProfileRequestDto request,
        CancellationToken cancellationToken = default);
}
