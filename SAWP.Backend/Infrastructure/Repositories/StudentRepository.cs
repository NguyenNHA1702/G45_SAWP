using Core.DTOs;
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

    public async Task<IReadOnlyList<ChildSummaryDto>> GetChildrenByGuardianUserIdAsync(int userId, CancellationToken ct = default)
    {
        var items = await _context.StudentGuardians.AsNoTracking()
            .Where(sg => sg.Guardian.UserId == userId && sg.Student.Status == "Active")
            .Select(sg => new ChildSummaryDto
            {
                StudentId = sg.Student.Id,
                StudentCode = sg.Student.StudentCode,
                FullName = sg.Student.FullName,
                AvatarUrl = sg.Student.AvatarUrl,
                Gender = sg.Student.Gender,
                DateOfBirth = sg.Student.DateOfBirth,
                ClassId = sg.Student.ClassAssignments.Where(ca => ca.IsActive).Select(ca => ca.ClassId).FirstOrDefault(),
                ClassName = sg.Student.ClassAssignments.Where(ca => ca.IsActive).Select(ca => ca.Class.ClassName).FirstOrDefault() ?? "Chưa phân lớp",
                Relationship = sg.Relationship,
                IsPrimaryContact = sg.IsPrimaryContact
            })
            .OrderBy(c => c.FullName)
            .ToListAsync(ct);

        return items;
    }
}
