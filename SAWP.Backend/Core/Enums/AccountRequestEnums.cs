namespace Core.Enums;

/// <summary>Why Office Staff ask for Parent accounts (BR-67).</summary>
public enum AccountRequestReason
{
    NewSchoolYearIntake = 1,
    AdditionalGuardian = 2,
    Correction = 3
}

/// <summary>Where the guardian rows of an account request came from (ERD row 28).</summary>
public enum AccountRequestSource
{
    Manual = 1,
    Import = 2
}

/// <summary>State of a whole account request (UC-67, UC-68).</summary>
public enum AccountRequestStatus
{
    Draft = 1,
    Submitted = 2,
    InReview = 3,
    Completed = 4,
    PartlyCompleted = 5,
    Rejected = 6,
    Closed = 7
}

/// <summary>State of one guardian row of an account request (ERD row 29, BR-67).</summary>
public enum AccountRequestItemStatus
{
    Waiting = 1,
    AccountCreated = 2,
    Active = 3,
    Rejected = 4,
    Failed = 5,
    Closed = 6
}

/// <summary>Result of the automatic checks on one guardian row (UC-67 E1, BR-64, BR-68).</summary>
public enum AccountRequestCheckResult
{
    Passed = 1,
    AlreadyHasAccount = 2,
    InOpenRequest = 3,
    MissingEmail = 4,
    InvalidEmail = 5,
    EmailOfStaffAccount = 6,
    EmailInUse = 7
}
