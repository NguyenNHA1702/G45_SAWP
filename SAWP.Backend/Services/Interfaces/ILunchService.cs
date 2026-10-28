using Core.DTOs;
using Core.Enums;

namespace Services.Interfaces;

public interface ILunchService
{
    Task<ApiResponse<bool>> RegisterLunchAsync(LunchRegistrationDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<DailyMealCountReportDto>> GetDailyMealCountAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<ApiResponse<MealHeadcountDto>> GetMealHeadcountAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-32: Lấy cấu hình ngày phục vụ trong khoảng ngày
    /// </summary>
    Task<ApiResponse<List<LunchServiceDayConfigDto>>> GetServiceDaysAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-32: Nhân viên bán trú cấu hình ngày phục vụ, hạn đăng ký/hủy và ghi chú hoặc thực đơn
    /// </summary>
    Task<ApiResponse<List<LunchServiceDayConfigDto>>> UpsertServiceDaysAsync(int userId, UpsertLunchServiceDaysRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-31: Phụ huynh (hoặc nhân sự có quyền) xem lịch và trạng thái đăng ký bán trú của học sinh theo tháng
    /// </summary>
    Task<ApiResponse<StudentLunchScheduleDto>> GetStudentScheduleAsync(int studentId, string month, int userId, IReadOnlyList<UserRoles> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-29: Phụ huynh chọn một hoặc nhiều ngày còn mở và xác nhận đăng ký độc lập cho từng ngày
    /// </summary>
    Task<ApiResponse<BatchLunchRegistrationResultDto>> RegisterLunchBatchAsync(int userId, IReadOnlyList<UserRoles> roles, BatchLunchRegistrationRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-30: Phụ huynh hủy đăng ký còn hiệu lực trước hạn hủy
    /// </summary>
    Task<ApiResponse<bool>> CancelRegistrationByParentAsync(int userId, IReadOnlyList<UserRoles> roles, CancelLunchRegistrationRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhận hủy suất ăn (nguồn: đơn nghỉ - WFL 1.2.3) cho ngày áp dụng trước hạn chốt
    /// </summary>
    Task<ApiResponse<bool>> CancelMealForLeaveAsync(int studentId, DateTime mealDate, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khôi phục hoặc tạo cảnh báo bổ sung suất ăn khi học sinh có đơn nghỉ nhưng thực tế vẫn đi học
    /// </summary>
    Task<ApiResponse<bool>> RestoreMealForStudentAsync(int studentId, DateTime mealDate, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-33: Lấy bảng vận hành & báo cáo bán trú theo ngày (phân quyền theo role và lọc lớp)
    /// </summary>
    Task<ApiResponse<DailyMealBoardDto>> GetDailyMealBoardAsync(
        DateTime date,
        int? userId = null,
        IReadOnlyList<UserRoles>? roles = null,
        int? classId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chốt số lượng suất ăn trong ngày gửi bếp ăn (giờ chốt: CutoffTime của ngày hoặc School:LunchCutoffTime)
    /// </summary>
    Task<ApiResponse<bool>> LockDailyMealAsync(LockMealDayRequestDto request, CancellationToken cancellationToken = default);
}
