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

    /// <summary>
    /// Assignment in a Draft school year (UC-69, BR-72). It is not the student's current class (IsActive is false)
    /// until the year is activated, when it becomes active.
    /// </summary>
    public bool IsDraft { get; set; }
}
