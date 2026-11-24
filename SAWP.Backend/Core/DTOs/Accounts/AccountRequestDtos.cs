namespace Core.DTOs;

// UC-67 Request Parent Accounts / UC-68 Review Account Requests (SSAWP-153).
// Statuses and check results travel as enum names (e.g. "Waiting", "EmailOfStaffAccount").

/// <summary>A guardian record without an account (UC-67 step 1, "Guardians Without Account").</summary>
public class GuardianWithoutAccountDto
{
    public int GuardianId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public List<string> Students { get; set; } = new();
    public string CheckResult { get; set; } = string.Empty;
    public string? CheckMessage { get; set; }
    public bool CanRequest { get; set; }
}

/// <summary>Result of the automatic checks on one guardian row (UC-67 E1, UC-68 step 2).</summary>
public class AccountRequestRowCheckDto
{
    public int GuardianId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string CheckResult { get; set; } = string.Empty;
    public string? Message { get; set; }
    public bool Passed => CheckResult == "Passed";
}

public class CheckAccountRequestRowsDto
{
    public List<int> GuardianIds { get; set; } = new();
}

/// <summary>Create or update a request; <see cref="Submit"/> false keeps it as a draft (UC-67 A4).</summary>
public class SaveAccountRequestDto
{
    public string Title { get; set; } = string.Empty;
    /// <summary>NewSchoolYearIntake, AdditionalGuardian or Correction (BR-67).</summary>
    public string Reason { get; set; } = "NewSchoolYearIntake";
    public string? Note { get; set; }
    public List<int> GuardianIds { get; set; } = new();
    /// <summary>Manual or Import (UC-09 A5).</summary>
    public string Source { get; set; } = "Manual";
    public string? SourceReference { get; set; }
    public bool Submit { get; set; }
}

public class AccountRequestItemDto
{
    public int Id { get; set; }
    public int GuardianId { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    /// <summary>The email when the row was added.</summary>
    public string? Email { get; set; }
    /// <summary>The guardian record's email now (it may have been corrected, UC-11).</summary>
    public string? CurrentEmail { get; set; }
    public List<string> Students { get; set; } = new();
    /// <summary>Waiting, AccountCreated, Active, EmailFailed, Rejected, Failed or Closed.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Fresh check result while the row is Waiting or Draft.</summary>
    public string? CheckResult { get; set; }
    public string? CheckMessage { get; set; }
    public string? Reason { get; set; }
    public int? CreatedUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class AccountRequestDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    /// <summary>Draft, Submitted, InReview, Completed, PartlyCompleted, Rejected or Closed.</summary>
    public string Status { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public int? FollowsUpRequestId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalRows { get; set; }
    public int WaitingRows { get; set; }
    public int CreatedRows { get; set; }
    public int RejectedRows { get; set; }
    public int FailedRows { get; set; }
    public int ClosedRows { get; set; }
    public List<AccountRequestItemDto>? Items { get; set; }
}

public class SaveAccountRequestResultDto
{
    public AccountRequestDto Request { get; set; } = new();
    /// <summary>Rows left out of a submitted request because a check failed (UC-67 E1).</summary>
    public List<AccountRequestRowCheckDto> ExcludedRows { get; set; } = new();
}

/// <summary>UC-68 step 3: approve or reject rows; one reason can cover several rejected rows.</summary>
public class AccountRequestDecisionDto
{
    public List<int> ApproveItemIds { get; set; } = new();
    public List<int> RejectItemIds { get; set; } = new();
    public string? RejectReason { get; set; }
    public bool ApproveAllValid { get; set; }
    /// <summary>A1: reject every waiting row with one reason.</summary>
    public bool RejectAll { get; set; }
}

public class AccountRequestDecisionResultDto
{
    public AccountRequestDto Request { get; set; } = new();
    public int AccountsCreated { get; set; }
    public int EmailFailed { get; set; }
    public int Failed { get; set; }
    public int Rejected { get; set; }
    /// <summary>Rows that failed a check and were not approved (UC-68 E1).</summary>
    public List<AccountRequestRowCheckDto> NotApproved { get; set; } = new();
}

public class AccountRequestRowsDto
{
    public List<int> ItemIds { get; set; } = new();
    public string? Note { get; set; }
}

/// <summary>UC-67 A3 / UC-11 A2: resend the activation link, optionally to a corrected email address.</summary>
public class ResendActivationDto
{
    public string? Email { get; set; }
}
