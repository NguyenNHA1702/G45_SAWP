namespace Core.DTOs;

public class GeneratePlacementPlanRequestDto
{
    public int SchoolYearId { get; set; }
    public int Grade { get; set; }
    public string? PlanName { get; set; }
    public List<int>? TargetClassIds { get; set; }
    public int TargetCapacity { get; set; } = 35;

    // Constraints & criteria options
    public int MaxSpecialNeedsPerClass { get; set; } = 2;
    public bool AvoidDuplicateNames { get; set; } = true;
    public bool PrioritizeResidentialArea { get; set; } = true;
    public bool BalanceGender { get; set; } = true;

    // Seamless auto-creation if target grade has no classes yet
    public bool AutoCreateClassesIfMissing { get; set; } = false;
    public int AutoCreateClassCount { get; set; } = 3;
    public List<string>? AutoCreateSectionSuffixes { get; set; }
}

public class PlacementStudentItemDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string? ResidentialArea { get; set; }
    public bool IsSpecialNeeds { get; set; }
    public string? SpecialNeedsNotes { get; set; }
    public bool IsManualOverride { get; set; }
    public string? Notes { get; set; }
}

public class PlacementPlanClassPreviewDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public int Capacity { get; set; }
    public int TotalStudents { get; set; }
    public int MaleCount { get; set; }
    public int FemaleCount { get; set; }
    public int SpecialNeedsCount { get; set; }
    public Dictionary<string, int> ResidentialAreaCounts { get; set; } = new();
    public List<PlacementStudentItemDto> Students { get; set; } = new();
}

public class PlacementCriteriaWarningDto
{
    /// <summary>
    /// "Warning", "Violation", or "Info"
    /// </summary>
    public string Severity { get; set; } = "Warning";
    public int? ClassId { get; set; }
    public string? ClassName { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class PlacementPlanSummaryDto
{
    public int Id { get; set; }
    public int SchoolYearId { get; set; }
    public string SchoolYearName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Submitted, Approved, Rejected
    public int TargetClassCount { get; set; }
    public int TargetCapacity { get; set; }
    public int TotalStudents { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public bool HasWarnings { get; set; }
    public int ViolationCount { get; set; }
}

public class PlacementPlanDetailDto : PlacementPlanSummaryDto
{
    public List<PlacementPlanClassPreviewDto> Classes { get; set; } = new();
    public List<PlacementCriteriaWarningDto> CriteriaWarnings { get; set; } = new();
}

public class MoveStudentInPlanDto
{
    public int StudentId { get; set; }
    public int FromClassId { get; set; }
    public int ToClassId { get; set; }
    public string? Notes { get; set; }
}

public class SwapStudentsInPlanDto
{
    public int Student1Id { get; set; }
    public int Class1Id { get; set; }
    public int Student2Id { get; set; }
    public int Class2Id { get; set; }
    public string? Notes { get; set; }
}

public class ReviewPlacementPlanDto
{
    public bool IsApproved { get; set; }
    public string? ReviewNotes { get; set; }
}
