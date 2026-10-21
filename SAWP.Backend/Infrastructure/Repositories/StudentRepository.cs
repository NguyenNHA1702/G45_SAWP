using Core.Entities;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class StudentRepository : Repository<Student>, IStudentRepository
{
    public StudentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Student?> GetStudentWithProfileAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .Include(s => s.StudentGuardians)
                .ThenInclude(sg => sg.Guardian)
                    .ThenInclude(g => g.User)
            .Include(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
                    .ThenInclude(c => c.SchoolYear)
            .Include(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
                    .ThenInclude(c => c.HomeroomTeacher)
                        .ThenInclude(t => t!.User)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);
    }


    public async Task<IReadOnlyList<Student>> GetStudentsByClassIdAsync(int classId, CancellationToken cancellationToken = default)
    {
        return await _context.ClassAssignments
            .Where(ca => ca.ClassId == classId && ca.IsActive)
            .Include(ca => ca.Student)
            .Select(ca => ca.Student)
            .ToListAsync(cancellationToken);
    }

    public async Task<Student?> GetByStudentCodeAsync(string studentCode, CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .Include(s => s.StudentGuardians)
                .ThenInclude(sg => sg.Guardian)
                    .ThenInclude(g => g.User)
            .Include(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
                    .ThenInclude(c => c.HomeroomTeacher)
                        .ThenInclude(t => t!.User)
            .FirstOrDefaultAsync(s => s.StudentCode == studentCode, cancellationToken);
    }

    // Workflow 2 additions
    public async Task<IReadOnlyList<Student>> GetActiveRosterAsync(int classId, DateTime date, CancellationToken ct = default)
    {
        return await _context.ClassAssignments.AsNoTracking()
            .Where(ca => ca.ClassId == classId 
                      && ca.IsActive
                      && ca.StartDate <= date 
                      && (ca.EndDate == null || ca.EndDate >= date)
                      && ca.Student.Status == "Active")
            .Select(ca => ca.Student)
            .OrderBy(s => s.FullName)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<int, List<int>>> GetGuardianUserIdsAsync(IEnumerable<int> studentIds, CancellationToken ct = default)
    {
        var rawPairs = await _context.StudentGuardians.AsNoTracking()
            .Where(sg => studentIds.Contains(sg.StudentId) && sg.Guardian.User.IsActive)
            .Select(sg => new { sg.StudentId, sg.Guardian.UserId })
            .ToListAsync(ct);

        return rawPairs
            .GroupBy(x => x.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.UserId).Distinct().ToList()
            );
    }

    public async Task<bool> IsGuardianOfAsync(int studentId, int userId, CancellationToken ct = default)
    {
        return await _context.StudentGuardians
            .AnyAsync(sg => sg.StudentId == studentId && sg.Guardian.UserId == userId, ct);
    }

    public async Task<bool> ExistsByStudentCodeAsync(string studentCode, int? excludeStudentId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Students.Where(s => s.StudentCode == studentCode);
        if (excludeStudentId.HasValue)
        {
            query = query.Where(s => s.Id != excludeStudentId.Value);
        }
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Student>> GetLinkedStudentsByParentUserIdAsync(int parentUserId, CancellationToken cancellationToken = default)
    {
        return await _context.StudentGuardians
            .Where(sg => sg.Guardian.UserId == parentUserId)
            .Include(sg => sg.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                    .ThenInclude(ca => ca.Class)
                        .ThenInclude(c => c.HomeroomTeacher)
                            .ThenInclude(t => t!.User)
            .Select(sg => sg.Student)
            .ToListAsync(cancellationToken);
    }
}

