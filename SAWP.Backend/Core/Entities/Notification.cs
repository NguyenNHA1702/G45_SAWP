namespace Core.Entities;

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public string? LinkUrl { get; set; }
    public string Type { get; set; } = "General";       // max 50, e.g. "AttendanceReattendanceRequest"
    public string? ReferenceKey { get; set; }           // max 100, a key that prevents duplicate notifications for the same event
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
