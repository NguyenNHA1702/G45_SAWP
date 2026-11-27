namespace Core.Entities;

public class SchoolYear
{
    public int Id { get; set; }
    public string YearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Draft, Active or Archived (<see cref="Core.Constants.SchoolYearStatuses"/>); IsActive is true only for Active (BR-14, BR-72).</summary>
    public string Status { get; set; } = Core.Constants.SchoolYearStatuses.Active;

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    public virtual ICollection<SchoolCalendarDay> CalendarDays { get; set; } = new List<SchoolCalendarDay>();
}
