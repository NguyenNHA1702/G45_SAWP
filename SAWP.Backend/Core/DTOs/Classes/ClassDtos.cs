namespace Core.DTOs;

public class CreateSchoolYearDto
{
    public string YearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SchoolYearDto
{
    public int Id { get; set; }
    public string YearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public int ClassCount { get; set; }
}

public class CreateClassDto
{
    public int SchoolYearId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string? RoomNumber { get; set; }
    public int Capacity { get; set; } = 35;
}

public class UpdateClassDto
{
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string? RoomNumber { get; set; }
    public int Capacity { get; set; } = 35;
    public string Status { get; set; } = "Active";
}

public class ClassSummaryDto
{
    public int Id { get; set; }
    public int SchoolYearId { get; set; }
    public string SchoolYearName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string? RoomNumber { get; set; }
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsPlacementPublished { get; set; }
    public int? HomeroomTeacherId { get; set; }
    public string? HomeroomTeacherName { get; set; }
    public string? HomeroomTeacherStaffCode { get; set; }
}

public class InitializeBatchClassesDto
{
    public int SchoolYearId { get; set; }
    public int? TargetGrade { get; set; }
    public List<int>? Grades { get; set; }
    public List<string>? SectionNames { get; set; }
    public List<string>? SectionSuffixes { get; set; }
    public int? CapacityPerClass { get; set; }
    public int DefaultCapacity { get; set; } = 35;
}

public class AssignTeacherDto
{
    public int StaffProfileId { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

public class TeacherOptionDto
{
    public int StaffProfileId { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string StaffCode { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? CurrentHomeroomClassName { get; set; }
    public bool HasActiveHomeroom { get; set; }
}

public class AssignStudentsDto
{
    public List<int> StudentIds { get; set; } = new();
}

public class RosterStudentItemDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? AvatarUrl { get; set; }
    public string? ResidentialArea { get; set; }
    public bool IsSpecialNeeds { get; set; }
    public string? SpecialNeedsNotes { get; set; }
    public DateTime EnrolledDate { get; set; }
    public string? PrimaryGuardianName { get; set; }
    public string? PrimaryGuardianPhone { get; set; }
    public string? PrimaryGuardianRelationship { get; set; }
}

public class ClassRosterDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public int Capacity { get; set; }
    public int TotalEnrolled { get; set; }
    public bool IsPlacementPublished { get; set; }
    public string? HomeroomTeacherName { get; set; }
    public string? HomeroomTeacherPhone { get; set; }
    public string? HomeroomTeacherEmail { get; set; }
    public List<RosterStudentItemDto> Students { get; set; } = new();
}

public class PublishPlacementResultDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int EnrolledCount { get; set; }
    public string? HomeroomTeacherName { get; set; }
    public DateTime PublishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
