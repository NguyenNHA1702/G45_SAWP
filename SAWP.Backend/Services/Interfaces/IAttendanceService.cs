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

    // API-5: GET /api/attendance/classes/{classId}/summary?from=&to=
    Task<ApiResponse<ClassAttendanceSummaryDto>> GetClassSummaryAsync(int classId, DateTime from, DateTime to, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);

    // API-6: GET /api/attendance/students/{studentId}/history?from=&to=
    Task<ApiResponse<StudentAttendanceHistoryDto>> GetStudentHistoryAsync(int studentId, DateTime from, DateTime to, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct = default);
}
