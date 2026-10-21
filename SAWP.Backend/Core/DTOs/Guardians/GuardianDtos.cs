using Core.Enums;

namespace Core.DTOs;

public class SubmitChildLinkDto
{
    public string StudentCode { get; set; } = string.Empty;
    public DateTime StudentDateOfBirth { get; set; }
    public RelationshipTypes Relationship { get; set; } = RelationshipTypes.Father;
    public string GuardianPhone { get; set; } = string.Empty;
    public string? GuardianIdentityCard { get; set; }
    public string? ProofDocumentUrl { get; set; }
    public string? Notes { get; set; }
}

public class ChildLinkRequestResultDto
{
    public int RequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public bool IsAutoVerified { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
}

public class ChildLinkRequestItemDto
{
    public int RequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public DateTime SubmissionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RequesterUserId { get; set; }
    public string RequesterFullName { get; set; } = string.Empty;
    public string RequesterPhone { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string? StudentFullName { get; set; }
    public DateTime? StudentDateOfBirth { get; set; }
    public int Relationship { get; set; }
    public string RelationshipName { get; set; } = string.Empty;
    public string? GuardianPhone { get; set; }
    public string? GuardianIdentityCard { get; set; }
    public string? ProofDocumentUrl { get; set; }
    public string? Notes { get; set; }
    public bool AutoVerificationPassed { get; set; }
    public string? VerificationDetails { get; set; }
    public string? ReviewerNotes { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string? ReviewerFullName { get; set; }
}

public class VerifyChildLinkDto
{
    public bool IsApproved { get; set; }
    public string ReviewerNotes { get; set; } = string.Empty;
    public RelationshipTypes? Relationship { get; set; }
    public bool IsPrimaryContact { get; set; } = true;
}

public class LinkedChildSummaryDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = "Active";
    public int? CurrentClassId { get; set; }
    public string? CurrentClassName { get; set; }
    public int? Grade { get; set; }
    public bool IsPlacementPublished { get; set; }
    public string RelationshipName { get; set; } = string.Empty;
    public bool IsPrimaryContact { get; set; }
}

public class HomeroomTeacherContactDto
{
    public string TeacherName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? StaffCode { get; set; }
}

public class AttendanceMiniSummaryDto
{
    public int TotalTrackedDays { get; set; }
    public int PresentDays { get; set; }
    public int ExcusedAbsenceDays { get; set; }
    public int UnexcusedAbsenceDays { get; set; }
    public int LateDays { get; set; }
    public double AttendanceRatePercentage { get; set; }
}

public class LunchStatusTodayDto
{
    public bool IsServiceAvailable { get; set; }
    public bool IsRegistered { get; set; }
    public string? MealType { get; set; }
    public string? CutoffTimeMessage { get; set; }
}

public class ChildDashboardDto
{
    public StudentProfileDto Student { get; set; } = new();
    public ClassSummaryDto? ClassInfo { get; set; }
    public HomeroomTeacherContactDto? HomeroomTeacher { get; set; }
    public AttendanceMiniSummaryDto AttendanceSummary { get; set; } = new();
    public LunchStatusTodayDto LunchStatusToday { get; set; } = new();
    public List<StudentGuardianItemDto> Guardians { get; set; } = new();
}
