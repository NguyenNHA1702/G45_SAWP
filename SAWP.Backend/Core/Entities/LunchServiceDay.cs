namespace Core.Entities;

public class LunchServiceDay
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public bool IsServing { get; set; } = true;
    public TimeSpan CutoffTime { get; set; } = new TimeSpan(8, 30, 0);
    public string? Remarks { get; set; }
}
