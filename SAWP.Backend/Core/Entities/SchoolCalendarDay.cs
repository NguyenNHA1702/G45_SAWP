namespace Core.Entities;

public class SchoolCalendarDay
{
    public int Id { get; set; }
    public int SchoolYearId { get; set; }
    public virtual SchoolYear SchoolYear { get; set; } = null!;

    public DateTime Date { get; set; }
    public bool IsSchoolDay { get; set; } = true;
    public string? Note { get; set; }
}
