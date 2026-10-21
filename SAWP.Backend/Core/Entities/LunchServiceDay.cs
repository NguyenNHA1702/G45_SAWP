namespace Core.Entities;

public class LunchServiceDay
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public bool IsServing { get; set; } = true;
    public TimeSpan CutoffTime { get; set; } = new TimeSpan(8, 30, 0);
    public bool IsLocked { get; set; } = false;
    public DateTime? LockedAt { get; set; }
    public string? Remarks { get; set; }
}
