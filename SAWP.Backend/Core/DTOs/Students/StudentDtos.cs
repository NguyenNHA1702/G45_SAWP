namespace Core.DTOs;

public class GuardianInputDto
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? IdentityCardNumber { get; set; }
    public string? Occupation { get; set; }
    public string? Address { get; set; }
    public int Relationship { get; set; } = 3; // RelationshipTypes.Guardian = 3
    public bool IsPrimaryContact { get; set; } = true;
}

public class CreateStudentDto
{
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public int? InitialClassId { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Status { get; set; } = "Active";
    public string? ResidentialArea { get; set; }
    public bool IsSpecialNeeds { get; set; } = false;
    public string? SpecialNeedsNotes { get; set; }
    public GuardianInputDto? InitialGuardian { get; set; }
}

public class UpdateStudentDto
{
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? AvatarUrl { get; set; }
    public string? ResidentialArea { get; set; }
    public bool IsSpecialNeeds { get; set; } = false;
    public string? SpecialNeedsNotes { get; set; }
    public int? CurrentClassId { get; set; }
}

public class StudentGuardianItemDto
{
    public int GuardianId { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? IdentityCardNumber { get; set; }
    public string? Occupation { get; set; }
    public string? Address { get; set; }
    public int Relationship { get; set; }
    public string RelationshipName { get; set; } = string.Empty;
    public bool IsPrimaryContact { get; set; }
}

public class StudentProfileDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public DateTime EnrollmentDate { get; set; }
    public string Status { get; set; } = "Active";
    public string? AvatarUrl { get; set; }
    public string? ResidentialArea { get; set; }
    public bool IsSpecialNeeds { get; set; }
    public string? SpecialNeedsNotes { get; set; }
    public int? CurrentClassId { get; set; }
    public string? CurrentClassName { get; set; }
    public int? CurrentGrade { get; set; }
    public string? CurrentRoomNumber { get; set; }
    public string? HomeroomTeacherName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<StudentGuardianItemDto> Guardians { get; set; } = new();
}

public class StudentFilterDto
{
    public string? SearchTerm { get; set; }
    public int? Grade { get; set; }
    public int? ClassId { get; set; }
    public string? Status { get; set; }
    public int? SchoolYearId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class DuplicateCheckResultDto
{
    public bool IsDuplicate { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public StudentProfileDto? ExistingStudent { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ResolveDuplicateStudentDto
{
    public string StudentCode { get; set; } = string.Empty;
    /// <summary>
    /// Action: "Merge", "Overwrite", or "Rename"
    /// </summary>
    public string Action { get; set; } = "Merge";
    public string? NewStudentCode { get; set; }
    public CreateStudentDto ProfileData { get; set; } = new();
}

public class ImportStudentRowDto
{
    public int RowNumber { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string? ResidentialArea { get; set; }
    public bool IsSpecialNeeds { get; set; } = false;
    public string? SpecialNeedsNotes { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    public string GuardianPhone { get; set; } = string.Empty;
    public string GuardianIdentityCard { get; set; } = string.Empty;
    public string Relationship { get; set; } = "Phụ huynh";
    public bool IsValid { get; set; } = true;
    public bool IsDuplicate { get; set; } = false;
    public string? ErrorMessage { get; set; }
}

public class ImportStudentsConfirmDto
{
    public List<ImportStudentRowDto> ValidRows { get; set; } = new();
    public int? SchoolYearId { get; set; }
}

public class ImportStudentsResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> ImportedStudentCodes { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
