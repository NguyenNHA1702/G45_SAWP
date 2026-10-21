using Core.Enums;

namespace Core.DTOs;

// ===== Request =====
public class DailyAttendanceEntryDto
{
    public int StudentId { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }                  // max 500
}

public class SubmitAttendanceSheetDto
{
    public DateTime Date { get; set; }                    // yyyy-MM-dd
    public AttendanceSession Session { get; set; } = AttendanceSession.Morning;
    public List<DailyAttendanceEntryDto> Entries { get; set; } = new();
}

public class CorrectAttendanceDto
{
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
    public string CorrectionReason { get; set; } = string.Empty;   // required, 5..500
}

// ===== Response =====
public class TeacherClassOptionDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string AccessReason { get; set; } = string.Empty;       // "Homeroom" | "FirstPeriod" | "SchoolAdmin"
    public bool IsSubmitted { get; set; }                          // đã điểm danh ngày đó chưa
}

public class LeaveIndicatorDto
{
    public int LeaveRequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public RequestStatus Status { get; set; }                      // Submitted | UnderReview | Approved
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public LeaveSession Session { get; set; }                      // Morning | Afternoon | FullDay
    public string Reason { get; set; } = string.Empty;
}

public class AttendanceSheetEntryDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int? RecordId { get; set; }                             // null = chưa điểm danh
    public AttendanceStatus? Status { get; set; }
    public AttendanceStatus SuggestedStatus { get; set; }          // ExcusedAbsence nếu có đơn Approved, ngược lại Present
    public string? Remarks { get; set; }
    public LeaveIndicatorDto? Leave { get; set; }
    public bool HasLunch { get; set; }                             // đang có suất ăn active
    public LunchCancellationSource? LunchCancelledBy { get; set; }
}

public class AttendanceSummaryDto
{
    public int Total { get; set; }
    public int Present { get; set; }
    public int Late { get; set; }
    public int ExcusedAbsence { get; set; }
    public int UnexcusedAbsence { get; set; }
    public int NotMarked { get; set; }
}

public class AttendanceSheetDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public AttendanceSession Session { get; set; }
    public bool IsSchoolDay { get; set; }
    public bool CanEdit { get; set; }                              // tính theo user + ngày
    public string? LockReason { get; set; }                        // "PastDay" | "NotAssigned" | "NonSchoolDay" | "Future"
    public DateTime? SubmittedAt { get; set; }                     // UTC, lần đầu
    public string? SubmittedByName { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public List<AttendanceSheetEntryDto> Entries { get; set; } = new();
    public AttendanceSummaryDto Summary { get; set; } = new();
}

public class AttendanceSubmitResultDto
{
    public AttendanceSummaryDto Summary { get; set; } = new();
    public int LunchDeductedCount { get; set; }
    public int LunchRestoredCount { get; set; }
    public int LunchRestoreSkippedCount { get; set; }              // sau giờ bếp chốt, không khôi phục được
    public int ParentsNotifiedCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class AttendanceDayDto
{
    public DateTime Date { get; set; }
    public AttendanceSession Session { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
    public string? LeaveRequestNumber { get; set; }
}

public class StudentAttendanceHistoryDto
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<AttendanceDayDto> Days { get; set; } = new();
    public AttendanceSummaryDto Summary { get; set; } = new();
}

public class ClassAttendanceSummaryDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int SchoolDays { get; set; }
    public AttendanceSummaryDto Totals { get; set; } = new();
    public decimal AttendanceRate { get; set; }                    // (Present+Late)/(tổng bản ghi) * 100, 2 số lẻ
    public List<StudentAttendanceRowDto> Students { get; set; } = new();
}

public class StudentAttendanceRowDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Present { get; set; }
    public int Late { get; set; }
    public int ExcusedAbsence { get; set; }
    public int UnexcusedAbsence { get; set; }
}
