using Core.Entities;

namespace Core.Interfaces;

public interface IAttendanceRepository : IRepository<AttendanceRecord>
{
    Task<IReadOnlyList<AttendanceRecord>> GetDailyClassAttendanceAsync(int classId, DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceRecord>> GetStudentAttendanceHistoryAsync(int studentId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}
