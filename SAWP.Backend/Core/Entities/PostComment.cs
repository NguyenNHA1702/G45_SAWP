namespace Core.Entities;

/// <summary>
/// Bình luận tương tác dưới bài đăng lớp học
/// </summary>
public class PostComment
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public virtual ClassPost Post { get; set; } = null!;

    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
