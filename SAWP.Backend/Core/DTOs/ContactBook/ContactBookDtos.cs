namespace Core.DTOs.ContactBook;

public class ClassPostDto
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public bool IsPinned { get; set; }
    public bool AllowComments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public int ReactionCount { get; set; }
    public bool UserHasReacted { get; set; }
    public string? UserReactionType { get; set; }

    public List<PostAttachmentDto> Attachments { get; set; } = new();
    public List<PostCommentDto> Comments { get; set; } = new();
}

public class PostAttachmentDto
{
    public int Id { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = "Image";
    public long FileSizeBytes { get; set; }
}

public class PostCommentDto
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public int UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorRoleLabel { get; set; } = string.Empty; // e.g. "GVCN", "Phụ huynh em Nguyễn Văn An"
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool CanDelete { get; set; }
}

public class CreateClassPostDto
{
    public int ClassId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public bool IsPinned { get; set; }
    public bool AllowComments { get; set; } = true;
    public List<string>? AttachmentUrls { get; set; }
}

public class CreatePostCommentDto
{
    public string Content { get; set; } = string.Empty;
}

public class ToggleReactionDto
{
    public string ReactionType { get; set; } = "Heart";
}

public class StudentEvaluationDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int TeacherUserId { get; set; }
    public string TeacherName { get; set; } = string.Empty;

    public DateTime EvaluationDate { get; set; }
    public int WeekNumber { get; set; }
    public string Rating { get; set; } = "Good";
    public List<string> Badges { get; set; } = new();
    public string TeacherNote { get; set; } = string.Empty;
    public string? PrivateRemind { get; set; }

    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedByGuardianName { get; set; }
    public string? ParentFeedback { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateStudentEvaluationDto
{
    public int StudentId { get; set; }
    public int ClassId { get; set; }
    public DateTime EvaluationDate { get; set; }
    public int WeekNumber { get; set; }
    public string Rating { get; set; } = "Good";
    public List<string> Badges { get; set; } = new();
    public string TeacherNote { get; set; } = string.Empty;
    public string? PrivateRemind { get; set; }
}

public class AcknowledgeEvaluationDto
{
    public string? ParentFeedback { get; set; }
}

public class StudentEvaluationRosterItemDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public StudentEvaluationDto? Evaluation { get; set; }
}
