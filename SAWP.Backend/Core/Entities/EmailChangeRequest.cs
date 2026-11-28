using Core.Enums;

namespace Core.Entities;

/// <summary>
/// Office Staff request to change a Parent's login email, reviewed by System Administrator (BR-69, UC-11 A2).
/// </summary>
public class EmailChangeRequest
{
    public int Id { get; set; }

    public int TargetUserId { get; set; }
    public virtual User TargetUser { get; set; } = null!;

    public int? GuardianId { get; set; }
    public virtual Guardian? Guardian { get; set; }

    public string OldEmail { get; set; } = string.Empty;
    public string NewEmail { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public EmailChangeRequestStatus Status { get; set; } = EmailChangeRequestStatus.Pending;

    public int RequestedByUserId { get; set; }
    public virtual User RequestedByUser { get; set; } = null!;

    public int? ReviewedByUserId { get; set; }
    public virtual User? ReviewedByUser { get; set; }
    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
