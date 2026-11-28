using Core.Enums;

namespace Core.Entities;

public class AttendanceReattendanceRequest
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceSession Session { get; set; }
    public int? RequestedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ReattendanceRequestStatus Status { get; set; } = ReattendanceRequestStatus.Pending;
    public int? ReviewedByUserId { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnlockedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Class Class { get; set; } = null!;
    public User? RequestedByUser { get; set; }
    public User? ReviewedByUser { get; set; }
}
