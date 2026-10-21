using Core.DTOs;

namespace Services.Interfaces;

public interface ILunchService
{
    Task<ApiResponse<bool>> RegisterLunchAsync(LunchRegistrationDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<DailyMealCountReportDto>> GetDailyMealCountAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<ApiResponse<MealHeadcountDto>> GetMealHeadcountAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhận hủy suất ăn trước giờ chốt 08h15 cho ngày áp dụng
    /// </summary>
    Task<ApiResponse<bool>> CancelMealForLeaveAsync(int studentId, DateTime mealDate, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khôi phục hoặc tạo cảnh báo bổ sung suất ăn khi học sinh có đơn nghỉ nhưng thực tế vẫn đi học
    /// </summary>
    Task<ApiResponse<bool>> RestoreMealForStudentAsync(int studentId, DateTime mealDate, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy bảng vận hành bán trú thời gian thực (Live Board) cho nhân viên bán trú / nhà bếp
    /// </summary>
    Task<ApiResponse<DailyMealBoardDto>> GetDailyMealBoardAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chốt số lượng suất ăn trong ngày gửi bếp ăn lúc 08h15
    /// </summary>
    Task<ApiResponse<bool>> LockDailyMealAsync(LockMealDayRequestDto request, CancellationToken cancellationToken = default);
}
