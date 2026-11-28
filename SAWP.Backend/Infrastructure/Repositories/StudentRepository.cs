using Core.Common;
using Core.DTOs;
using Core.Enums;
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
            .AsSplitQuery()
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
            .AsNoTracking()
            .Where(ca => ca.ClassId == classId && ca.IsActive)
            .Include(ca => ca.Student)
            .Select(ca => ca.Student)
            .ToListAsync(cancellationToken);
    }

    public async Task<Student?> GetByStudentCodeAsync(string studentCode, CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .AsSplitQuery()
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
        var dayStart = date.Date;
        var nextDay = dayStart.AddDays(1);

        return await _context.ClassAssignments.AsNoTracking()
            .Where(ca => ca.ClassId == classId
                      && ca.IsActive
                      && ca.StartDate < nextDay
                      && (ca.EndDate == null || ca.EndDate >= dayStart)
                      && ca.Student.Status == "Active")
            .Select(ca => ca.Student)
            .Distinct()
            .OrderBy(s => s.FullName)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<int, List<int>>> GetGuardianUserIdsAsync(IEnumerable<int> studentIds, CancellationToken ct = default)
    {
        var rawPairs = await _context.StudentGuardians.AsNoTracking()
            .Where(sg => studentIds.Contains(sg.StudentId) && sg.Guardian.User != null && sg.Guardian.User.Status == AccountStatus.Active)
            .Select(sg => new { sg.StudentId, UserId = sg.Guardian.UserId!.Value })
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
            .AsNoTracking()
            .AsSplitQuery()
            .Where(sg => sg.Guardian.UserId == parentUserId)
            .Include(sg => sg.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                    .ThenInclude(ca => ca.Class)
                        .ThenInclude(c => c.HomeroomTeacher)
                            .ThenInclude(t => t!.User)
            .Select(sg => sg.Student)
            .ToListAsync(cancellationToken);
    }

    public async Task<StudentProfileLookupResult?> GetStudentProfileDtoAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var row = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new
            {
                s.Id,
                s.StudentCode,
                s.FullName,
                s.DateOfBirth,
                s.Gender,
                s.EnrollmentDate,
                s.Status,
                s.AvatarUrl,
                s.ResidentialArea,
                s.IsSpecialNeeds,
                s.SpecialNeedsNotes,
                s.CreatedAt,
                ActiveClass = s.ClassAssignments
                    .Where(ca => ca.IsActive && (ca.Class.SchoolYear == null || ca.Class.SchoolYear.IsActive))
                    .Select(ca => new
                    {
                        ca.Class.Id,
                        ca.Class.ClassName,
                        ca.Class.Grade,
                        ca.Class.RoomNumber,
                        HomeroomTeacherUserId = ca.Class.HomeroomTeacher != null ? (int?)ca.Class.HomeroomTeacher.UserId : null,
                        HomeroomTeacherName = ca.Class.HomeroomTeacher != null && ca.Class.HomeroomTeacher.User != null
                            ? ca.Class.HomeroomTeacher.User.FullName
                            : null
                    })
                    .ToList(),
                Guardians = s.StudentGuardians
                    .Select(sg => new
                    {
                        sg.GuardianId,
                        sg.Guardian.UserId,
                        sg.Guardian.FullName,
                        sg.Guardian.PhoneNumber,
                        sg.Guardian.Email,
                        sg.Guardian.IdentityCardNumber,
                        sg.Guardian.Occupation,
                        sg.Guardian.Address,
                        Relationship = (int)sg.Relationship,
                        RelationshipName = sg.Relationship.ToString(),
                        sg.IsPrimaryContact
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row == null)
        {
            return null;
        }

        var activeClass = row.ActiveClass.FirstOrDefault();
        var primaryGuardian = row.Guardians.FirstOrDefault(g => g.IsPrimaryContact) ?? row.Guardians.FirstOrDefault();

        return new StudentProfileLookupResult
        {
            GuardianUserIds = row.Guardians.Where(g => g.UserId.HasValue).Select(g => g.UserId!.Value).Distinct().ToList(),
            HomeroomTeacherUserIds = row.ActiveClass.Where(c => c.HomeroomTeacherUserId.HasValue).Select(c => c.HomeroomTeacherUserId!.Value).Distinct().ToList(),
            Profile = new StudentProfileDto
            {
                Id = row.Id,
                StudentCode = row.StudentCode,
                FullName = row.FullName,
                DateOfBirth = row.DateOfBirth,
                Gender = row.Gender,
                EnrollmentDate = row.EnrollmentDate,
                Status = row.Status,
                AvatarUrl = row.AvatarUrl,
                Address = row.ResidentialArea ?? primaryGuardian?.Address,
                ResidentialArea = row.ResidentialArea,
                IsSpecialNeeds = row.IsSpecialNeeds,
                SpecialNeedsNotes = row.SpecialNeedsNotes,
                CurrentClassId = activeClass?.Id,
                CurrentClassName = activeClass?.ClassName,
                CurrentGrade = activeClass?.Grade,
                CurrentRoomNumber = activeClass?.RoomNumber,
                HomeroomTeacherName = activeClass?.HomeroomTeacherName,
                PrimaryGuardianName = primaryGuardian?.FullName,
                PrimaryGuardianPhone = primaryGuardian?.PhoneNumber,
                GuardiansCount = row.Guardians.Count,
                CreatedAt = row.CreatedAt,
                Guardians = row.Guardians.Select(g => new StudentGuardianItemDto
                {
                    GuardianId = g.GuardianId,
                    UserId = g.UserId,
                    FullName = g.FullName,
                    PhoneNumber = g.PhoneNumber,
                    Email = g.Email,
                    IdentityCardNumber = g.IdentityCardNumber,
                    Occupation = g.Occupation,
                    Address = g.Address,
                    Relationship = g.Relationship,
                    RelationshipName = g.RelationshipName,
                    IsPrimaryContact = g.IsPrimaryContact
                }).ToList()
            }
        };
    }

    public async Task<List<LinkedChildSummaryDto>?> GetLinkedChildDtosAsync(int parentUserId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.StudentGuardians
            .AsNoTracking()
            .Where(sg => sg.Guardian.UserId == parentUserId)
            .Select(sg => new
            {
                sg.Student.Id,
                sg.Student.StudentCode,
                sg.Student.FullName,
                sg.Student.DateOfBirth,
                sg.Student.Gender,
                sg.Student.AvatarUrl,
                sg.Student.Status,
                sg.Relationship,
                sg.IsPrimaryContact,
                ActiveClass = sg.Student.ClassAssignments
                    .Where(ca => ca.IsActive && (ca.Class.SchoolYear == null || ca.Class.SchoolYear.IsActive))
                    .Select(ca => new
                    {
                        ca.ClassId,
                        ca.Class.ClassName,
                        ca.Class.Grade,
                        HomeroomTeacherName = ca.Class.HomeroomTeacher != null && ca.Class.HomeroomTeacher.User != null
                            ? ca.Class.HomeroomTeacher.User.FullName
                            : null,
                        HomeroomTeacherPhone = ca.Class.HomeroomTeacher != null && ca.Class.HomeroomTeacher.User != null
                            ? ca.Class.HomeroomTeacher.User.PhoneNumber
                            : null
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new LinkedChildSummaryDto
        {
            StudentId = r.Id,
            StudentCode = r.StudentCode,
            FullName = r.FullName,
            DateOfBirth = r.DateOfBirth,
            Gender = r.Gender,
            AvatarUrl = r.AvatarUrl,
            Status = r.Status,
            CurrentClassId = r.ActiveClass?.ClassId,
            CurrentClassName = r.ActiveClass?.ClassName,
            Grade = r.ActiveClass?.Grade,
            HomeroomTeacherName = r.ActiveClass?.HomeroomTeacherName,
            HomeroomTeacherPhone = r.ActiveClass?.HomeroomTeacherPhone,
            RelationshipName = r.Relationship.ToVietnamese(),
            IsPrimaryContact = r.IsPrimaryContact
        }).ToList();
    }
}

