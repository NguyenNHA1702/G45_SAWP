using Core.DTOs;
using Core.Enums;

namespace Services.Interfaces;

public interface IAttendanceService
{
    // API-1: GET /api/attendance/classes?date=&session=
    Task<ApiResponse<List<TeacherClassOptionDto>>> GetMyClassesAsync(int userId, IReadOnlyList<UserRoles> roles, DateTime? date, AttendanceSession session, CancellationToken ct = default);

    // API-2: GET /api/attendance/classes/{classId}?date=&session=
    Task<ApiResponse<AttendanceSheetDto>> GetAttendanceSheetAsync(int classId, DateTime date, AttendanceSession session, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);

    // API-3: POST /api/attendance/classes/{classId}
    Task<ApiResponse<AttendanceSubmitResultDto>> RecordDailyAttendanceAsync(int classId, SubmitAttendanceSheetDto dto, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);

    // API-4: PUT /api/attendance/records/{recordId}
    Task<ApiResponse<AttendanceSheetEntryDto>> CorrectAttendanceAsync(int recordId, CorrectAttendanceDto dto, int userId, CancellationToken ct = default);

    // OVR-10 (UC-23 A1): contact notes of one attendance record (school staff with access to the class)
    Task<ApiResponse<List<AbsenceContactNoteDto>>> GetContactNotesAsync(int recordId, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);

    // OVR-10 (UC-23 A1): the homeroom Teacher or Office Staff record a contact note on an unexcused absence (MSG55)
    Task<ApiResponse<AbsenceContactNoteDto>> AddContactNoteAsync(int recordId, CreateAbsenceContactNoteDto dto, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);

    // OVR-10 (UC-23 A1): the note's author or Office Staff mark a wrong note superseded (MSG319)
    Task<ApiResponse<AbsenceContactNoteDto>> SupersedeContactNoteAsync(int noteId, SupersedeAbsenceContactNoteDto dto, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);

    // OVR-10 (UC-23 A2): Office Staff list the unexcused absences of a date range with no contact note in effect
    Task<ApiResponse<List<AbsenceWithoutContactNoteDto>>> GetAbsencesWithoutContactNoteAsync(DateTime from, DateTime to, int? classId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);

    // API-5: GET /api/attendance/classes/{classId}/summary?from=&to=
    Task<ApiResponse<ClassAttendanceSummaryDto>> GetClassSummaryAsync(int classId, DateTime from, DateTime to, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);

    // API-5b: GET /api/attendance/classes/{classId}/matrix?from=&to=
    Task<ApiResponse<ClassAttendanceMatrixDto>> GetClassMatrixAsync(int classId, DateTime from, DateTime to, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);

    // API-6: GET /api/attendance/students/{studentId}/history?from=&to=
    Task<ApiResponse<StudentAttendanceHistoryDto>> GetStudentHistoryAsync(int studentId, DateTime from, DateTime to, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);

    // POST /api/attendance/classes/{classId}/request-reattendance
    Task<ApiResponse<bool>> RequestReattendanceAsync(int classId, RequestReattendanceDto dto, int requesterUserId, CancellationToken ct = default);

    Task<ApiResponse<List<ReattendanceRequestDto>>> GetReattendanceRequestsAsync(string? status = null, CancellationToken ct = default);
    Task<ApiResponse<ReattendanceRequestDto>> UnlockAttendanceSheetAsync(int classId, UnlockAttendanceSheetDto dto, int staffUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> LockAttendanceSheetAsync(int classId, LockAttendanceSheetDto dto, int staffUserId, CancellationToken ct = default);
    Task<ApiResponse<ReattendanceRequestDto>> RejectReattendanceRequestAsync(int requestId, RejectReattendanceRequestDto dto, int staffUserId, CancellationToken ct = default);
}
