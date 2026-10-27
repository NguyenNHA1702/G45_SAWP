namespace Core.Entities;

/// <summary>
/// Tệp đính kèm bài viết lớp học (ảnh hoạt động, tài liệu dặn dò)
/// </summary>
public class PostAttachment
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public virtual ClassPost Post { get; set; } = null!;

    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = "Image"; // Image, Document
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
