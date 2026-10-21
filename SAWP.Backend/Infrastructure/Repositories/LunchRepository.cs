using Core.Entities;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class LunchRepository : Repository<LunchRegistration>, ILunchRepository
{
    public LunchRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<LunchRegistration>> GetDailyRegistrationsAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.LunchRegistrations
            .Include(lr => lr.Student)
            .Where(lr => lr.Date.Date == date.Date && lr.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<LunchRegistration?> GetStudentRegistrationForDateAsync(int studentId, DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.LunchRegistrations
            .FirstOrDefaultAsync(lr => lr.StudentId == studentId && lr.Date == date, cancellationToken);
    }

    public async Task<Dictionary<int, LunchRegistration>> GetRegistrationsForStudentsAsync(
        IEnumerable<int> studentIds, 
        DateTime date, 
        bool track = false, 
        CancellationToken ct = default)
    {
        var idList = studentIds.ToList();
        IQueryable<LunchRegistration> query = _context.LunchRegistrations;
        if (!track) query = query.AsNoTracking();

        return await query
            .Where(r => idList.Contains(r.StudentId) && r.Date == date)
            .ToDictionaryAsync(r => r.StudentId, ct);
    }

    public async Task<IReadOnlyList<HeadcountRow>> GetHeadcountRowsAsync(DateTime date, CancellationToken ct = default)
    {
        var query = from r in _context.LunchRegistrations.AsNoTracking()
                    where r.Date == date
                    join ca in _context.ClassAssignments.AsNoTracking() on r.StudentId equals ca.StudentId
                    where ca.IsActive && ca.StartDate <= date && (ca.EndDate == null || ca.EndDate >= date)
                    select new HeadcountRow(r.StudentId, ca.ClassId, ca.Class.ClassName, r.MealType, r.IsActive, r.CancellationSource);

        return await query.ToListAsync(ct);
    }
}

