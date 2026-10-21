using Core.Entities;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ClassScheduleRepository : Repository<ClassSchedule>, IClassScheduleRepository
{
    public ClassScheduleRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<int?> GetFirstPeriodTeacherIdAsync(int classId, DateTime date, CancellationToken ct = default)
    {
        var dow = date.DayOfWeek;

        return await _context.ClassSchedules
            .AsNoTracking()
            .Where(s => s.ClassId == classId 
                     && s.IsActive 
                     && s.DayOfWeek == dow
                     && s.EffectiveFrom <= date 
                     && (s.EffectiveTo == null || s.EffectiveTo >= date))
            .OrderBy(s => s.Period)
            .Select(s => (int?)s.TeacherStaffId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<int>> GetClassesWhereTeacherHasFirstPeriodAsync(int staffId, DateTime date, CancellationToken ct = default)
    {
        var dow = date.DayOfWeek;

        var schedulesForDay = await _context.ClassSchedules
            .AsNoTracking()
            .Where(s => s.IsActive 
                     && s.DayOfWeek == dow
                     && s.EffectiveFrom <= date 
                     && (s.EffectiveTo == null || s.EffectiveTo >= date))
            .ToListAsync(ct);

        var firstPeriodClassIds = schedulesForDay
            .GroupBy(s => s.ClassId)
            .Where(g => g.OrderBy(s => s.Period).First().TeacherStaffId == staffId)
            .Select(g => g.Key)
            .ToList();

        return firstPeriodClassIds;
    }
}

