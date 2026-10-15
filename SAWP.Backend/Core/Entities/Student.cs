namespace Core.Entities;

public class Student
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Active";
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
    public virtual ICollection<ClassAssignment> ClassAssignments { get; set; } = new List<ClassAssignment>();
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public virtual ICollection<LunchRegistration> LunchRegistrations { get; set; } = new List<LunchRegistration>();
    public virtual ICollection<AdministrativeRequest> AdministrativeRequests { get; set; } = new List<AdministrativeRequest>();
}
