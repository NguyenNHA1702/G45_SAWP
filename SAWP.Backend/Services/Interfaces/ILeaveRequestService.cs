using Core.DTOs;
using Core.Enums;

namespace Services.Interfaces;

public interface ILeaveRequestService
{
    /// <summary>
    /// Phụ huynh nộp đơn xin nghỉ học
    /// </summary>
    Task<ApiResponse<LeaveRequestItemDto>> SubmitLeaveRequestAsync(int parentUserId, CreateLeaveRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phụ huynh xem danh sách đơn xin nghỉ của các con
    /// </summary>
    Task<ApiResponse<List<LeaveRequestItemDto>>> GetMyLeaveRequestsAsync(int parentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phụ huynh xem danh sách đơn xin nghỉ của các con (phân trang)
    /// </summary>
    Task<ApiResponse<PagedLeaveRequestsDto>> GetMyLeaveRequestsPagedAsync(int parentUserId, int? studentId = null, string? status = null, int pageIndex = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// GVCN xem danh sách đơn xin nghỉ của lớp mình phụ trách
    /// </summary>
    Task<ApiResponse<List<LeaveRequestItemDto>>> GetClassLeaveRequestsAsync(int teacherUserId, int? classId = null, string? status = null, DateTime? date = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// GVCN / BGH xem danh sách đơn xin nghỉ (phân trang + thống kê)
    /// </summary>
    Task<ApiResponse<PagedLeaveRequestsDto>> GetClassLeaveRequestsPagedAsync(int teacherUserId, int? classId = null, string? status = null, DateTime? date = null, string? search = null, int pageIndex = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xem chi tiết một đơn xin nghỉ
    /// </summary>
    Task<ApiResponse<LeaveRequestItemDto>> GetLeaveRequestDetailsAsync(int requestId, int requestingUserId, IReadOnlyList<UserRoles>? requestingRoles = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// GVCN duyệt (Approve) hoặc từ chối (Reject) đơn xin nghỉ kèm kích hoạt đồng bộ
    /// </summary>
    Task<ApiResponse<SyncLeaveResultDto>> ReviewLeaveRequestAsync(int requestId, int reviewerUserId, ReviewLeaveRequestDto review, IReadOnlyList<UserRoles>? reviewerRoles = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phụ huynh chỉnh sửa và gửi lại đơn xin nghỉ (khi đơn ở trạng thái Submitted hoặc NeedMoreInfo)
    /// </summary>
    Task<ApiResponse<LeaveRequestItemDto>> UpdateLeaveRequestAsync(int requestId, int parentUserId, UpdateLeaveRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phụ huynh hủy đơn khi đơn còn ở trạng thái Chờ duyệt hoặc Yêu cầu bổ sung thông tin
    /// </summary>
    Task<ApiResponse<bool>> CancelLeaveRequestAsync(int requestId, int parentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phụ huynh thu hồi đơn đã duyệt cho các ngày chưa qua giờ chốt
    /// </summary>
    Task<ApiResponse<bool>> WithdrawLeaveRequestAsync(int requestId, int parentUserId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra (dung lượng, định dạng) và lưu tệp đính kèm của đơn xin nghỉ
    /// </summary>
    Task<ApiResponse<AttachmentUploadResultDto>> UploadAttachmentAsync(Stream content, string fileName, long length, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mở tệp đính kèm đã lưu; trả về null nếu không tồn tại hoặc không có quyền truy cập
    /// </summary>
    Task<AttachmentFileDto?> GetAttachmentAsync(string path, int? requestingUserId = null, IReadOnlyList<UserRoles>? requestingRoles = null, CancellationToken cancellationToken = default);
}
