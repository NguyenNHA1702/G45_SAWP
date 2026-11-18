namespace Core.Entities;

public class LunchMonthlyQuota
{
    public int Id { get; set; }
    public string Month { get; set; } = string.Empty; // YYYY-MM
    public int Quota { get; set; }
    public int? UpdatedByUserId { get; set; }
    public virtual User? UpdatedByUser { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
