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

