namespace Core.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string? LinkUrl { get; set; }
    public string Type { get; set; } = "General";
    public string? ReferenceKey { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UnreadCountDto
{
    public int Count { get; set; }
}

/// <summary>
/// GAP-03 (UC-47 / UC-65): Office Staff or School Board broadcasts a notification to All/Role/Grade/Class.
/// </summary>
public class BroadcastNotificationRequestDto
{
    public string TargetScope { get; set; } = "All"; // All | Role | Grade | Class
    public string? TargetRole { get; set; }
    public int? Grade { get; set; }
    public int? ClassId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
}

public class BroadcastNotificationResultDto
{
    public int RecipientCount { get; set; }
    public string ReferenceKey { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

public class BroadcastHistoryItemDto
{
    public string ReferenceKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int RecipientCount { get; set; }
    public int ReadCount { get; set; }
    public DateTime SentAt { get; set; }
}
