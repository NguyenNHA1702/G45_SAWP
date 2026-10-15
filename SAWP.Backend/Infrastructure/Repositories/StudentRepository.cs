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
            .Include(s => s.ClassAssignments)
                .ThenInclude(ca => ca.Class)
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
            .FirstOrDefaultAsync(s => s.StudentCode == studentCode, cancellationToken);
    }
}
