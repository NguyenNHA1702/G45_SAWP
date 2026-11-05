namespace Core.Entities;

/// <summary>
/// Biểu tượng cảm xúc (Reaction) trên bài đăng lớp học
/// </summary>
public class PostReaction
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public virtual ClassPost Post { get; set; } = null!;

    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string ReactionType { get; set; } = "Heart"; // Heart, Like, Clap
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
