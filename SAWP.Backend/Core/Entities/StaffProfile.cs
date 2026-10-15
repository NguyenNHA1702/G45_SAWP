namespace Core.Entities;

public class StaffProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string StaffCode { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool IsHomeroomTeacher { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Class> HomeroomClasses { get; set; } = new List<Class>();
}
