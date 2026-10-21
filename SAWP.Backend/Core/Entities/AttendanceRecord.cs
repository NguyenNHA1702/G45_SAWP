using Core.Enums;

namespace Core.Entities;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public int ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public DateTime Date { get; set; }                 // CHỈ phần ngày (00:00), theo giờ trường
    public AttendanceSession Session { get; set; } = AttendanceSession.Morning;
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }               // max 500

    public int RecordedByUserId { get; set; }
    public virtual User RecordedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- thêm mới cho WF 2 ----
    public DateTime? UpdatedAt { get; set; }           // UTC
    public int? UpdatedByUserId { get; set; }
    public virtual User? UpdatedByUser { get; set; }
    public string? CorrectionReason { get; set; }      // max 500, bắt buộc khi Giáo vụ sửa
    public int? LeaveRequestId { get; set; }           // đơn nghỉ đã duyệt phủ ngày này
    public virtual LeaveRequest? LeaveRequest { get; set; }
}
