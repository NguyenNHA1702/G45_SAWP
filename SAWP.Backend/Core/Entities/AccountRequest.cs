using Core.Enums;

namespace Core.Entities;

/// <summary>A Parent account request sent by Office Staff and reviewed by a System Administrator (priv ERD row 28, UC-67, UC-68).</summary>
public class AccountRequest
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public AccountRequestReason Reason { get; set; }
    public string? Note { get; set; }

    public int RequestedByUserId { get; set; }
    public virtual User RequestedByUser { get; set; } = null!;

    public AccountRequestSource Source { get; set; } = AccountRequestSource.Manual;

    /// <summary>Which import the rows came from (e.g. the import file name), when <see cref="Source"/> is Import.</summary>
    public string? SourceReference { get; set; }

    /// <summary>The request whose rejected or failed rows this one resubmits (BR-67).</summary>
    public int? FollowsUpRequestId { get; set; }
    public virtual AccountRequest? FollowsUpRequest { get; set; }

    public AccountRequestStatus Status { get; set; } = AccountRequestStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual ICollection<AccountRequestItem> Items { get; set; } = new List<AccountRequestItem>();
}
