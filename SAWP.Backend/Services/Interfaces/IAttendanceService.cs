using Core.DTOs;
using Core.Enums;

namespace Services.Interfaces;

public interface IAttendanceService
{
    /// <summary>
    /// Lấy danh sách điểm danh của lớp theo ngày và buổi (Sáng/Chiều), tự động tích hợp dữ liệu đơn nghỉ đã duyệt
    /// </summary>
    Task<ApiResponse<ClassDailySheetDto>> GetClassDailySheetAsync(int classId, DateTime date, AttendanceSession session, int requestingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// GVCN lưu điểm danh thực tế theo buổi, kiểm tra xung đột với đơn nghỉ và kích hoạt hủy suất ăn nếu vắng trước 08h15
    /// </summary>
    Task<ApiResponse<AttendanceSaveResultDto>> SaveClassAttendanceAsync(SaveClassAttendanceDto request, int teacherUserId, CancellationToken cancellationToken = default);
}
