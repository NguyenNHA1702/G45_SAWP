using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Enums;

namespace Core.DTOs;

public class RelationshipJsonConverter : JsonConverter<RelationshipTypes>
{
    public override RelationshipTypes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var val = reader.GetInt32();
            if (Enum.IsDefined(typeof(RelationshipTypes), val))
                return (RelationshipTypes)val;
            return RelationshipTypes.Father;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString()?.Trim().ToLowerInvariant() ?? string.Empty;
            if (int.TryParse(str, out var num) && Enum.IsDefined(typeof(RelationshipTypes), num))
                return (RelationshipTypes)num;

            if (str.Contains("bố") || str.Contains("cha") || str.Contains("father"))
                return RelationshipTypes.Father;
            if (str.Contains("mẹ") || str.Contains("me") || str.Contains("mother"))
                return RelationshipTypes.Mother;
            if (str.Contains("giám hộ") || str.Contains("giam ho") || str.Contains("guardian"))
                return RelationshipTypes.Guardian;

            return RelationshipTypes.Other;
        }

        return RelationshipTypes.Father;
    }

    public override void Write(Utf8JsonWriter writer, RelationshipTypes value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue((int)value);
    }
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
    public string? ClassName { get => CurrentClassName; set => CurrentClassName = value; }
    public int? Grade { get; set; }
    public string? HomeroomTeacherName { get; set; }
    public string? HomeroomTeacherPhone { get; set; }
    public string RelationshipName { get; set; } = string.Empty;
    public string Relationship { get => RelationshipName; set => RelationshipName = value; }
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
