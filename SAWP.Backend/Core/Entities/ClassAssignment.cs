namespace Core.Entities;

public class ClassAssignment
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public string AcademicYear { get; set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}
