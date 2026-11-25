using Core.DTOs;
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
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
    }

    public async Task<IReadOnlyList<Class>> GetClassesBySchoolYearAsync(int schoolYearId, int? grade = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Classes
            .Include(c => c.SchoolYear)
            .Include(c => c.HomeroomTeacher)
                .ThenInclude(t => t!.User)
            .Include(c => c.ClassAssignments.Where(ca => ca.IsActive))
            .AsSplitQuery()
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
            .AsNoTracking()
            .AsSplitQuery()
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
            query = query.Where(c => c.SchoolYear.IsActive);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsTeacherAssignedToActiveHomeroomAsync(int teacherProfileId, int schoolYearId, int? excludeClassId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Classes
            .AsNoTracking()
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
            .AsNoTracking()
            .Where(r => r.Name == UserRoles.Teacher.ToString())
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return await _context.StaffProfiles
            .AsNoTracking()
            .AsSplitQuery()
            .Include(sp => sp.User)
            .Include(sp => sp.HomeroomClasses.Where(c => c.SchoolYear.IsActive))
            .Where(sp => sp.User.Status == AccountStatus.Active && _context.UserRoles.Any(ur => ur.UserId == sp.UserId && ur.RoleId == teacherRoleId))
            .OrderBy(sp => sp.User.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Student>> GetEligibleStudentsForAssignmentAsync(int schoolYearId, int? grade = null, CancellationToken cancellationToken = default)
    {
        var assignedStudentIds = _context.ClassAssignments
            .AsNoTracking()
            .Where(ca => ca.IsActive && ca.Class.SchoolYearId == schoolYearId)
            .Select(ca => ca.StudentId);

        var query = _context.Students
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.StudentGuardians)
                .ThenInclude(sg => sg.Guardian)
                    .ThenInclude(g => g.User)
            .Where(s => s.Status == "Active" && !assignedStudentIds.Contains(s.Id));

        return await query
            .OrderBy(s => s.FullName)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SchoolYearDto>?> GetSchoolYearDtosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SchoolYears
            .AsNoTracking()
            .OrderByDescending(sy => sy.StartDate)
            .Select(sy => new SchoolYearDto
            {
                Id = sy.Id,
                YearName = sy.YearName,
                StartDate = sy.StartDate,
                EndDate = sy.EndDate,
                IsActive = sy.IsActive,
                ClassCount = sy.Classes.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SchoolYearDto?> GetActiveSchoolYearDtoAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SchoolYears
            .AsNoTracking()
            .Where(sy => sy.IsActive)
            .Select(sy => new SchoolYearDto
            {
                Id = sy.Id,
                YearName = sy.YearName,
                StartDate = sy.StartDate,
                EndDate = sy.EndDate,
                IsActive = sy.IsActive,
                ClassCount = sy.Classes.Count
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ClassSummaryDto>?> GetClassSummaryDtosAsync(int schoolYearId, int? grade = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Classes
            .AsNoTracking()
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
            .Select(c => new ClassSummaryDto
            {
                Id = c.Id,
                SchoolYearId = c.SchoolYearId,
                SchoolYearName = c.SchoolYear != null ? c.SchoolYear.YearName : string.Empty,
                ClassName = c.ClassName,
                Grade = c.Grade,
                RoomNumber = c.RoomNumber,
                Capacity = c.Capacity,
                EnrolledCount = c.ClassAssignments.Count(ca => ca.IsActive),
                Status = c.Status,
                IsPlacementPublished = c.IsPlacementPublished,
                HomeroomTeacherId = c.HomeroomTeacherId,
                HomeroomTeacherName = c.HomeroomTeacher != null && c.HomeroomTeacher.User != null ? c.HomeroomTeacher.User.FullName : null,
                HomeroomTeacherStaffCode = c.HomeroomTeacher != null ? c.HomeroomTeacher.StaffCode : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TeacherOptionDto>?> GetEligibleTeacherDtosAsync(int schoolYearId, CancellationToken cancellationToken = default)
    {
        var teacherRoleId = await _context.Roles
            .AsNoTracking()
            .Where(r => r.Name == UserRoles.Teacher.ToString())
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var teacherClassMap = await _context.Classes
            .AsNoTracking()
            .Where(c => c.SchoolYearId == schoolYearId && c.HomeroomTeacherId.HasValue)
            .Select(c => new { TeacherId = c.HomeroomTeacherId!.Value, c.ClassName })
            .ToListAsync(cancellationToken);

        var map = teacherClassMap
            .GroupBy(x => x.TeacherId)
            .ToDictionary(g => g.Key, g => g.First().ClassName);

        var teachers = await _context.StaffProfiles
            .AsNoTracking()
            .Where(sp => sp.User.Status == AccountStatus.Active && _context.UserRoles.Any(ur => ur.UserId == sp.UserId && ur.RoleId == teacherRoleId))
            .OrderBy(sp => sp.User.FullName)
            .Select(sp => new
            {
                sp.Id,
                sp.UserId,
                FullName = sp.User.FullName,
                sp.StaffCode,
                sp.Department
            })
            .ToListAsync(cancellationToken);

        return teachers.Select(t => new TeacherOptionDto
        {
            StaffProfileId = t.Id,
            UserId = t.UserId,
            FullName = t.FullName,
            StaffCode = t.StaffCode,
            Department = t.Department,
            CurrentHomeroomClassName = map.TryGetValue(t.Id, out var cName) ? cName : null,
            HasActiveHomeroom = map.ContainsKey(t.Id)
        }).ToList();
    }

    public async Task<PaginatedResult<RosterStudentItemDto>?> GetEligibleStudentsPagedAsync(
        int schoolYearId,
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, pageIndex);
        var size = Math.Clamp(pageSize, 1, 100);

        var query = _context.Students
            .AsNoTracking()
            .Where(s => s.Status == "Active"
                && !_context.ClassAssignments.Any(ca => ca.IsActive && ca.Class.SchoolYearId == schoolYearId && ca.StudentId == s.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s => s.StudentCode.ToLower().Contains(term) || s.FullName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0 || (page - 1) * size >= totalCount)
        {
            return new PaginatedResult<RosterStudentItemDto>(new List<RosterStudentItemDto>(), totalCount, page, size);
        }

        // Two-step fetch: IDs first so SQL Server only joins guardians for the 25 rows on this page
        var pageIds = await query
            .OrderBy(s => s.FullName)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var items = await _context.Students
            .AsNoTracking()
            .Where(s => pageIds.Contains(s.Id))
            .OrderBy(s => s.FullName)
            .ThenBy(s => s.Id)
            .Select(s => new RosterStudentItemDto
            {
                StudentId = s.Id,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                DateOfBirth = s.DateOfBirth,
                Gender = s.Gender,
                Status = s.Status,
                ResidentialArea = s.ResidentialArea,
                IsSpecialNeeds = s.IsSpecialNeeds,
                SpecialNeedsNotes = s.SpecialNeedsNotes,
                EnrolledDate = s.EnrollmentDate,
                PrimaryGuardianName = s.StudentGuardians
                    .Where(sg => sg.IsPrimaryContact && sg.Guardian != null)
                    .Select(sg => sg.Guardian.User != null ? sg.Guardian.User.FullName : sg.Guardian.FullName)
                    .FirstOrDefault(),
                PrimaryGuardianPhone = s.StudentGuardians
                    .Where(sg => sg.IsPrimaryContact && sg.Guardian != null)
                    .Select(sg => sg.Guardian.User != null ? sg.Guardian.User.PhoneNumber : sg.Guardian.PhoneNumber)
                    .FirstOrDefault(),
                PrimaryGuardianRelationship = s.StudentGuardians
                    .Where(sg => sg.IsPrimaryContact)
                    .Select(sg => sg.Relationship.ToString())
                    .FirstOrDefault(),
                EmergencyContactPhone = s.StudentGuardians
                    .Where(sg => !sg.IsPrimaryContact && sg.Guardian != null)
                    .Select(sg => sg.Guardian.User != null ? sg.Guardian.User.PhoneNumber : sg.Guardian.PhoneNumber)
                    .FirstOrDefault()
                    ?? s.StudentGuardians
                        .Where(sg => sg.IsPrimaryContact && sg.Guardian != null)
                        .Select(sg => sg.Guardian.User != null ? sg.Guardian.User.PhoneNumber : sg.Guardian.PhoneNumber)
                        .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new PaginatedResult<RosterStudentItemDto>(items, totalCount, page, size);
    }

    public Task<ClassRosterDto?> GetClassRosterDtoAsync(int classId, CancellationToken cancellationToken = default)
        => GetClassRosterWithAccessCheckAsync(classId, cancellationToken).ContinueWith(t => t.Result?.Roster, cancellationToken);

    public async Task<(ClassRosterDto Roster, int? HomeroomTeacherUserId)?> GetClassRosterWithAccessCheckAsync(int classId, CancellationToken cancellationToken = default)
    {
        var header = await _context.Classes
            .AsNoTracking()
            .Where(c => c.Id == classId)
            .Select(c => new
            {
                c.Id,
                c.ClassName,
                c.Grade,
                AcademicYear = c.SchoolYear != null ? c.SchoolYear.YearName : string.Empty,
                c.RoomNumber,
                c.Capacity,
                c.IsPlacementPublished,
                HomeroomTeacherUserId = c.HomeroomTeacher != null ? (int?)c.HomeroomTeacher.UserId : null,
                HomeroomTeacherName = c.HomeroomTeacher != null && c.HomeroomTeacher.User != null ? c.HomeroomTeacher.User.FullName : null,
                HomeroomTeacherPhone = c.HomeroomTeacher != null && c.HomeroomTeacher.User != null ? c.HomeroomTeacher.User.PhoneNumber : null,
                HomeroomTeacherEmail = c.HomeroomTeacher != null && c.HomeroomTeacher.User != null ? c.HomeroomTeacher.User.Email : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (header == null) return null;

        var students = await _context.ClassAssignments
            .AsNoTracking()
            .Where(ca => ca.ClassId == classId && ca.IsActive)
            .OrderBy(ca => ca.Student.FullName)
            .Select(ca => new RosterStudentItemDto
            {
                StudentId = ca.Student.Id,
                StudentCode = ca.Student.StudentCode,
                FullName = ca.Student.FullName,
                DateOfBirth = ca.Student.DateOfBirth,
                Gender = ca.Student.Gender,
                Status = ca.Student.Status,
                ResidentialArea = ca.Student.ResidentialArea,
                IsSpecialNeeds = ca.Student.IsSpecialNeeds,
                SpecialNeedsNotes = ca.Student.SpecialNeedsNotes,
                EnrolledDate = ca.Student.EnrollmentDate,
                PrimaryGuardianName = ca.Student.StudentGuardians
                    .Where(sg => sg.IsPrimaryContact && sg.Guardian != null)
                    .Select(sg => sg.Guardian.User != null ? sg.Guardian.User.FullName : sg.Guardian.FullName)
                    .FirstOrDefault(),
                PrimaryGuardianPhone = ca.Student.StudentGuardians
                    .Where(sg => sg.IsPrimaryContact && sg.Guardian != null)
                    .Select(sg => sg.Guardian.User != null ? sg.Guardian.User.PhoneNumber : sg.Guardian.PhoneNumber)
                    .FirstOrDefault(),
                PrimaryGuardianRelationship = ca.Student.StudentGuardians
                    .Where(sg => sg.IsPrimaryContact)
                    .Select(sg => sg.Relationship.ToString())
                    .FirstOrDefault(),
                EmergencyContactPhone = ca.Student.StudentGuardians
                    .Where(sg => !sg.IsPrimaryContact && sg.Guardian != null)
                    .Select(sg => sg.Guardian.User != null ? sg.Guardian.User.PhoneNumber : sg.Guardian.PhoneNumber)
                    .FirstOrDefault()
                    ?? ca.Student.StudentGuardians
                        .Where(sg => sg.IsPrimaryContact && sg.Guardian != null)
                        .Select(sg => sg.Guardian.User != null ? sg.Guardian.User.PhoneNumber : sg.Guardian.PhoneNumber)
                        .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var roster = new ClassRosterDto
        {
            ClassId = header.Id,
            ClassName = header.ClassName,
            Grade = header.Grade,
            AcademicYear = header.AcademicYear,
            RoomNumber = header.RoomNumber,
            Capacity = header.Capacity,
            TotalEnrolled = students.Count,
            IsPlacementPublished = header.IsPlacementPublished,
            HomeroomTeacherName = header.HomeroomTeacherName,
            HomeroomTeacherPhone = header.HomeroomTeacherPhone,
            HomeroomTeacherEmail = header.HomeroomTeacherEmail,
            Students = students
        };

        return (roster, header.HomeroomTeacherUserId);
    }

    public async Task<int?> GetHomeroomClassIdByTeacherUserIdAsync(int teacherUserId, int? schoolYearId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Classes
            .AsNoTracking()
            .Where(c => c.HomeroomTeacher != null && c.HomeroomTeacher.UserId == teacherUserId);

        query = schoolYearId.HasValue
            ? query.Where(c => c.SchoolYearId == schoolYearId.Value)
            : query.Where(c => c.SchoolYear.IsActive);

        return await query.Select(c => (int?)c.Id).FirstOrDefaultAsync(cancellationToken);
    }
}
