namespace Core.Entities;

public class ClassSchedule
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public int SchoolYearId { get; set; }
    public virtual SchoolYear SchoolYear { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }           // System.DayOfWeek (Monday = 1 ...)
    public byte Period { get; set; }                   // tiết 1..10, nhỏ nhất = tiết đầu
    public string Subject { get; set; } = string.Empty;// max 100

    public int TeacherStaffId { get; set; }
    public virtual StaffProfile TeacherStaff { get; set; } = null!;

    public DateTime EffectiveFrom { get; set; }        // date
    public DateTime? EffectiveTo { get; set; }         // date, null = còn hiệu lực
    public bool IsActive { get; set; } = true;
}
