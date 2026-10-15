using Core.Enums;

namespace Core.Entities;

public class AdministrativeRequest
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    public int RequesterUserId { get; set; }
    public virtual User RequesterUser { get; set; } = null!;

    public int? StudentId { get; set; }
    public virtual Student? Student { get; set; }

    public string RequestType { get; set; } = string.Empty;
    public RequestStatus Status { get; set; } = RequestStatus.Submitted;

    public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedDate { get; set; }

    public int? ReviewerUserId { get; set; }
    public virtual User? ReviewerUser { get; set; }

    public string? ReviewerNotes { get; set; }

    public virtual LeaveRequest? LeaveRequest { get; set; }
    public virtual ClassTransferDetail? ClassTransferDetail { get; set; }
    public virtual DocumentRequestDetail? DocumentRequestDetail { get; set; }
}
