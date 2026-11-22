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
    /// GVCN xem danh sách đơn xin nghỉ của lớp mình phụ trách
    /// </summary>
    Task<ApiResponse<List<LeaveRequestItemDto>>> GetClassLeaveRequestsAsync(int teacherUserId, int? classId = null, string? status = null, DateTime? date = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xem chi tiết một đơn xin nghỉ
    /// </summary>
    Task<ApiResponse<LeaveRequestItemDto>> GetLeaveRequestDetailsAsync(int requestId, int requestingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// GVCN duyệt (Approve) hoặc từ chối (Reject) đơn xin nghỉ kèm kích hoạt đồng bộ
    /// </summary>
    Task<ApiResponse<SyncLeaveResultDto>> ReviewLeaveRequestAsync(int requestId, int reviewerUserId, ReviewLeaveRequestDto review, IReadOnlyList<UserRoles>? reviewerRoles = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phụ huynh hủy đơn khi đơn còn ở trạng thái Pending
    /// </summary>
    Task<ApiResponse<bool>> CancelLeaveRequestAsync(int requestId, int parentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra (dung lượng, định dạng) và lưu tệp đính kèm của đơn xin nghỉ
    /// </summary>
    Task<ApiResponse<AttachmentUploadResultDto>> UploadAttachmentAsync(Stream content, string fileName, long length, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mở tệp đính kèm đã lưu; trả về null nếu không tồn tại
    /// </summary>
    Task<AttachmentFileDto?> GetAttachmentAsync(string path, CancellationToken cancellationToken = default);
}
