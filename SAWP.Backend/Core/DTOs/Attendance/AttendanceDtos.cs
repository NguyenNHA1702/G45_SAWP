using Core.Enums;

namespace Core.DTOs;

public class DailyAttendanceEntryDto
{
    public int StudentId { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
}

public class AttendanceRowDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public AttendanceStatus CurrentStatus { get; set; } = AttendanceStatus.Present;
    public bool HasApprovedLeave { get; set; }
    public string? LeaveReason { get; set; }
    public bool IsPreFilled { get; set; }
    public string? Remarks { get; set; }
}

public class ClassDailySheetDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public AttendanceSession Session { get; set; }
    public string SessionName => Session == AttendanceSession.Morning ? "Buổi sáng" : "Buổi chiều";
    public bool IsSaved { get; set; }
    public DateTime? SavedAt { get; set; }
    public string? SavedByName { get; set; }
    public int TotalStudents => Students.Count;
    public int PresentCount => Students.Count(s => s.CurrentStatus == AttendanceStatus.Present);
    public int ExcusedAbsenceCount => Students.Count(s => s.CurrentStatus == AttendanceStatus.ExcusedAbsence);
    public int UnexcusedAbsenceCount => Students.Count(s => s.CurrentStatus == AttendanceStatus.UnexcusedAbsence);
    public int LateCount => Students.Count(s => s.CurrentStatus == AttendanceStatus.Late);
    public List<AttendanceRowDto> Students { get; set; } = new();
}

public class SaveClassAttendanceDto
{
    public int ClassId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceSession Session { get; set; }
    public List<DailyAttendanceEntryDto> Entries { get; set; } = new();
}

public class AttendanceSaveResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int SavedCount { get; set; }
    public List<string> MealAlerts { get; set; } = new();
}
