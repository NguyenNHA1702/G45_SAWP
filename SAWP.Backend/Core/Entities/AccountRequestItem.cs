using Core.Enums;

namespace Core.Entities;

/// <summary>One guardian record in an account request (priv ERD row 29). Each row is decided on its own (BR-67).</summary>
public class AccountRequestItem
{
    public int Id { get; set; }

    public int AccountRequestId { get; set; }
    public virtual AccountRequest AccountRequest { get; set; } = null!;

    public int GuardianId { get; set; }
    public virtual Guardian Guardian { get; set; } = null!;

    /// <summary>The guardian record's email when the row was added.</summary>
    public string? Email { get; set; }

    public AccountRequestCheckResult? CheckResult { get; set; }
    public AccountRequestItemStatus Status { get; set; } = AccountRequestItemStatus.Waiting;

    /// <summary>The Administrator's or the system's reason for a rejected, failed or closed row.</summary>
    public string? Reason { get; set; }

    public int? CreatedUserId { get; set; }
    public virtual User? CreatedUser { get; set; }

    public int? DecidedByUserId { get; set; }
    public virtual User? DecidedByUser { get; set; }
    public DateTime? DecidedAt { get; set; }
}
