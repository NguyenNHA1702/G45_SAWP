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
    public bool IsPlacementPublished { get; set; } = false;

    public virtual ICollection<ClassAssignment> ClassAssignments { get; set; } = new List<ClassAssignment>();
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
