namespace Core.Entities;

public class Class
{
    public int Id { get; set; }
    public int SchoolYearId { get; set; }
    public virtual SchoolYear SchoolYear { get; set; } = null!;

    public int? HomeroomTeacherId { get; set; }
    public virtual StaffProfile? HomeroomTeacher { get; set; }

    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string? RoomNumber { get; set; }
    public int Capacity { get; set; } = 35;
    public string Status { get; set; } = "Active";

    /// <summary>Review mark of a class of a Draft school year (UC-14 A2, BR-72); any later change resets it.</summary>
    public bool IsReviewed { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }

    public virtual ICollection<ClassAssignment> ClassAssignments { get; set; } = new List<ClassAssignment>();
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
