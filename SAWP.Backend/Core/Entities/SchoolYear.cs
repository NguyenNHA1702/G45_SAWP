namespace Core.Entities;

public class SchoolYear
{
    public int Id { get; set; }
    public string YearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    public virtual ICollection<SchoolCalendarDay> CalendarDays { get; set; } = new List<SchoolCalendarDay>();
}
