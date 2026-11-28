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
    public string? CorrectionReason { get; set; }         // bắt buộc 5..500 khi Giáo vụ/Admin bổ sung/sửa ngày quá khứ (ATT-4)
}

public class CorrectAttendanceDto
{
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
    public string CorrectionReason { get; set; } = string.Empty;   // required, 5..500
}

public class RequestReattendanceDto
{
    public DateTime Date { get; set; }
    public AttendanceSession Session { get; set; } = AttendanceSession.Morning;
    public string? Reason { get; set; }
}

// ===== Response =====
public class TeacherClassOptionDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string? SchoolYearName { get; set; }
    public int StudentCount { get; set; }
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
    // OVR-10 (UC-23 A1): contact notes of the record, newest first; empty for roles that may not see them
    public List<AbsenceContactNoteDto> ContactNotes { get; set; } = new();
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
    public int NotMarked { get; set; }
}

// ===== OVR-10: contact notes on unexcused absences (UC-23 A1, A2; BR-62) =====
public class CreateAbsenceContactNoteDto
{
    public DateTime? ContactedAt { get; set; }                      // required; not in the future
    public ContactChannel? Channel { get; set; }                    // required
    public ContactOutcome? Outcome { get; set; }                    // required
    public string? Note { get; set; }                               // required, max 500
}

public class SupersedeAbsenceContactNoteDto
{
    public string? Reason { get; set; }                             // required, 5..500
}

public class AbsenceContactNoteDto
{
    public int Id { get; set; }
    public int AttendanceRecordId { get; set; }
    public DateTime ContactedAt { get; set; }                       // UTC
    public ContactChannel Channel { get; set; }
    public ContactOutcome Outcome { get; set; }
    public string Note { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }                         // UTC
    public bool IsSuperseded => SupersededAt != null;
    public DateTime? SupersededAt { get; set; }                     // UTC
    public string? SupersededByName { get; set; }
    public string? SupersedeReason { get; set; }
    /// <summary>The current user may mark it superseded: the note is in effect and the user is its author or Office Staff.</summary>
    public bool CanSupersede { get; set; }
}

/// <summary>One unexcused absence with no contact note in effect (UC-23 A2).</summary>
public class AbsenceWithoutContactNoteDto
{
    public int RecordId { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string? HomeroomTeacherName { get; set; }
    public DateTime Date { get; set; }
    public AttendanceSession Session { get; set; }
    public string? Remarks { get; set; }
    /// <summary>Notes that were marked superseded; the absence is listed again because none is in effect.</summary>
    public int SupersededNoteCount { get; set; }
}

public class ClassAttendanceMatrixDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<DateTime> Dates { get; set; } = new();
    public List<ClassMatrixStudentDto> Students { get; set; } = new();
}

public class ClassMatrixStudentDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
    public int Present { get; set; }
    public int Late { get; set; }
    public int ExcusedAbsence { get; set; }
    public int UnexcusedAbsence { get; set; }
    public List<ClassMatrixCellDto> Cells { get; set; } = new();
}

public class ClassMatrixCellDto
{
    public DateTime Date { get; set; }
    public AttendanceSession Session { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
    public string? LeaveRequestNumber { get; set; }
}
