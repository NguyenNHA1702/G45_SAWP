using Core.DTOs;
using Core.Entities;
using Core.Enums;

namespace Core.Interfaces;

public interface IAttendanceRepository : IRepository<AttendanceRecord>
{
    Task<IReadOnlyList<AttendanceRecord>> GetDailyClassAttendanceAsync(int classId, DateTime date, AttendanceSession session, bool track = false, CancellationToken cancellationToken = default);
    Task<AttendanceRecord?> GetByIdWithStudentClassAsync(int id, CancellationToken ct = default);
    Task<Dictionary<int, LeaveIndicatorDto>> GetActiveLeavesCoveringDateAsync(IEnumerable<int> studentIds, DateTime date, AttendanceSession session, CancellationToken ct = default);
    Task<IReadOnlyList<StudentAttendanceRowDto>> GetClassStatusCountsAsync(int classId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<HashSet<int>> GetSubmittedClassIdsAsync(IEnumerable<int> classIds, DateTime date, AttendanceSession session, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecord>> GetStudentAttendanceHistoryAsync(int studentId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}
