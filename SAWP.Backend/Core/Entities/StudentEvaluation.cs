namespace Core.Entities;

/// <summary>
/// Sổ nhận xét học sinh định kỳ / đột xuất (không quản lý điểm số)
/// </summary>
public class StudentEvaluation
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public int ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public int TeacherUserId { get; set; }
    public virtual User TeacherUser { get; set; } = null!;

    public DateTime EvaluationDate { get; set; }
    public int WeekNumber { get; set; } // Tuần 1 -> Tuần 35

    public string Rating { get; set; } = "Good"; // Excellent, Good, NeedsAttention
    public string BadgesJson { get; set; } = "[]"; // JSON array: ["ChamNgoan", "TichCuc", "AnNgoan"]
    public string TeacherNote { get; set; } = string.Empty;
    public string? PrivateRemind { get; set; }

    // Luồng xác nhận 2 chiều của Phụ huynh
    public bool IsAcknowledged { get; set; } = false;
    public DateTime? AcknowledgedAt { get; set; }
    public int? AcknowledgedByGuardianId { get; set; }
    public virtual Guardian? AcknowledgedByGuardian { get; set; }
    public string? ParentFeedback { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
