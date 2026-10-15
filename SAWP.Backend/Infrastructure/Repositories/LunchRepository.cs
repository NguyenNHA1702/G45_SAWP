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
            .FirstOrDefaultAsync(lr => lr.StudentId == studentId && lr.Date.Date == date.Date, cancellationToken);
    }
}
