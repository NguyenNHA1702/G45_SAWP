using Core.Entities;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class AttendanceRepository : Repository<AttendanceRecord>, IAttendanceRepository
{
    public AttendanceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetDailyClassAttendanceAsync(int classId, DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceRecords
            .Include(ar => ar.Student)
            .Where(ar => ar.ClassId == classId && ar.Date.Date == date.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetStudentAttendanceHistoryAsync(int studentId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceRecords
            .Where(ar => ar.StudentId == studentId && ar.Date.Date >= fromDate.Date && ar.Date.Date <= toDate.Date)
            .OrderByDescending(ar => ar.Date)
            .ToListAsync(cancellationToken);
    }
}
