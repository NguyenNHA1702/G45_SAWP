using System.Text.Json.Serialization;
using Core.Enums;

namespace Core.DTOs;

public class CreateStudentDto
{
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public int? InitialClassId { get; set; }
}

public class StudentProfileDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string CurrentClassName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class ImportStudentRowDto
{
    public int RowNumber { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string GuardianName { get; set; } = string.Empty;
    public string GuardianPhone { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class ChildSummaryDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Gender { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RelationshipTypes Relationship { get; set; }
    public bool IsPrimaryContact { get; set; }
}
