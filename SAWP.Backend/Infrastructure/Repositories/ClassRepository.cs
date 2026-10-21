using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ClassRepository : Repository<Class>, IClassRepository
{
    public ClassRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Class?> GetClassWithDetailsAsync(int classId, CancellationToken cancellationToken = default)
    {
        return await _context.Classes
            .Include(c => c.SchoolYear)
            .Include(c => c.HomeroomTeacher)
                .ThenInclude(t => t!.User)
            .Include(c => c.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Student)
                    .ThenInclude(s => s.StudentGuardians)
                        .ThenInclude(sg => sg.Guardian)
                            .ThenInclude(g => g.User)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
    }

    public async Task<IReadOnlyList<Class>> GetClassesBySchoolYearAsync(int schoolYearId, int? grade = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Classes
            .Include(c => c.SchoolYear)
            .Include(c => c.HomeroomTeacher)
                .ThenInclude(t => t!.User)
            .Include(c => c.ClassAssignments.Where(ca => ca.IsActive))
            .Where(c => c.SchoolYearId == schoolYearId);

        if (grade.HasValue)
        {
            query = query.Where(c => c.Grade == grade.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        return await query
            .OrderBy(c => c.Grade)
            .ThenBy(c => c.ClassName)
            .ToListAsync(cancellationToken);
    }

    public async Task<Class?> GetHomeroomClassByTeacherUserIdAsync(int teacherUserId, int? schoolYearId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Classes
            .Include(c => c.SchoolYear)
            .Include(c => c.HomeroomTeacher)
                .ThenInclude(t => t!.User)
            .Include(c => c.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Student)
                    .ThenInclude(s => s.StudentGuardians)
                        .ThenInclude(sg => sg.Guardian)
                            .ThenInclude(g => g.User)
            .Where(c => c.HomeroomTeacher != null && c.HomeroomTeacher.UserId == teacherUserId);

        if (schoolYearId.HasValue)
        {
            query = query.Where(c => c.SchoolYearId == schoolYearId.Value);
        }
        else
        {
            // Default to currently active school year
            query = query.Where(c => c.SchoolYear.IsActive);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsTeacherAssignedToActiveHomeroomAsync(int teacherProfileId, int schoolYearId, int? excludeClassId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Classes
            .Where(c => c.HomeroomTeacherId == teacherProfileId && c.SchoolYearId == schoolYearId);

        if (excludeClassId.HasValue)
        {
            query = query.Where(c => c.Id != excludeClassId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<SchoolYear?> GetActiveSchoolYearAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SchoolYears
            .Include(sy => sy.Classes)
            .FirstOrDefaultAsync(sy => sy.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<SchoolYear>> GetAllSchoolYearsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SchoolYears
            .Include(sy => sy.Classes)
            .OrderByDescending(sy => sy.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StaffProfile>> GetEligibleTeachersAsync(CancellationToken cancellationToken = default)
    {
        var teacherRoleId = await _context.Roles
            .Where(r => r.Name == UserRoles.Teacher.ToString())
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return await _context.StaffProfiles
            .Include(sp => sp.User)
            .Include(sp => sp.HomeroomClasses.Where(c => c.SchoolYear.IsActive))
            .Where(sp => sp.User.IsActive && _context.UserRoles.Any(ur => ur.UserId == sp.UserId && ur.RoleId == teacherRoleId))
            .OrderBy(sp => sp.User.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Student>> GetEligibleStudentsForAssignmentAsync(int schoolYearId, int? grade = null, CancellationToken cancellationToken = default)
    {
        // Enrolled students who do NOT have an active class assignment in this school year
        var assignedStudentIds = _context.ClassAssignments
            .Where(ca => ca.IsActive && ca.Class.SchoolYearId == schoolYearId)
            .Select(ca => ca.StudentId);

        var query = _context.Students
            .Include(s => s.StudentGuardians)
                .ThenInclude(sg => sg.Guardian)
                    .ThenInclude(g => g.User)
            .Where(s => s.Status == "Active" && !assignedStudentIds.Contains(s.Id));

        return await query
            .OrderBy(s => s.FullName)
            .ToListAsync(cancellationToken);
    }
}
