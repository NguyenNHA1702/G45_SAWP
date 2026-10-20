using Core.Enums;

namespace Core.Entities;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public int ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public DateTime Date { get; set; }
    public AttendanceSession Session { get; set; } = AttendanceSession.Morning;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    public int RecordedByUserId { get; set; }
    public virtual User RecordedByUser { get; set; } = null!;

    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
