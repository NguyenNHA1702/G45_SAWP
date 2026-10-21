using Core.DTOs;
using Core.Entities;

namespace Core.Interfaces;

public interface IAttendanceRepository : IRepository<AttendanceRecord>
{
    Task<IReadOnlyList<AttendanceRecord>> GetDailyClassAttendanceAsync(int classId, DateTime date, bool track = false, CancellationToken cancellationToken = default);
    Task<AttendanceRecord?> GetByIdWithStudentClassAsync(int id, CancellationToken ct = default);
    Task<Dictionary<int, LeaveIndicatorDto>> GetActiveLeavesCoveringDateAsync(IEnumerable<int> studentIds, DateTime date, CancellationToken ct = default);
    Task<IReadOnlyList<StudentAttendanceRowDto>> GetClassStatusCountsAsync(int classId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<HashSet<int>> GetSubmittedClassIdsAsync(IEnumerable<int> classIds, DateTime date, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecord>> GetStudentAttendanceHistoryAsync(int studentId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}
