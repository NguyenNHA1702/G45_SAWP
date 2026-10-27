namespace Core.Entities;

/// <summary>
/// Bài đăng trên bảng tin lớp học (Class Activity & Homework Feed)
/// </summary>
public class ClassPost
{
    public int Id { get; set; }

    public int ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public int AuthorUserId { get; set; }
    public virtual User AuthorUser { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // General, Homework, Activity, Welfare
    public bool IsPinned { get; set; } = false;
    public bool AllowComments { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<PostAttachment> Attachments { get; set; } = new List<PostAttachment>();
    public virtual ICollection<PostComment> Comments { get; set; } = new List<PostComment>();
    public virtual ICollection<PostReaction> Reactions { get; set; } = new List<PostReaction>();
}
