using Core.DTOs;
using Core.Entities;

namespace Core.Interfaces;

public interface IClassRepository : IRepository<Class>
{
    Task<Class?> GetClassWithDetailsAsync(int classId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Class>> GetClassesBySchoolYearAsync(int schoolYearId, int? grade = null, string? status = null, CancellationToken cancellationToken = default);
    Task<Class?> GetHomeroomClassByTeacherUserIdAsync(int teacherUserId, int? schoolYearId = null, CancellationToken cancellationToken = default);
    Task<bool> IsTeacherAssignedToActiveHomeroomAsync(int teacherProfileId, int schoolYearId, int? excludeClassId = null, CancellationToken cancellationToken = default);
    Task<SchoolYear?> GetActiveSchoolYearAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchoolYear>> GetAllSchoolYearsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffProfile>> GetEligibleTeachersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetEligibleStudentsForAssignmentAsync(int schoolYearId, int? grade = null, CancellationToken cancellationToken = default);

    // Fast read-only projections for high-volume datasets
    Task<List<SchoolYearDto>?> GetSchoolYearDtosAsync(CancellationToken cancellationToken = default) => Task.FromResult<List<SchoolYearDto>?>(null);
    Task<SchoolYearDto?> GetActiveSchoolYearDtoAsync(CancellationToken cancellationToken = default) => Task.FromResult<SchoolYearDto?>(null);
    Task<List<ClassSummaryDto>?> GetClassSummaryDtosAsync(int schoolYearId, int? grade = null, string? status = null, CancellationToken cancellationToken = default) => Task.FromResult<List<ClassSummaryDto>?>(null);
    Task<List<TeacherOptionDto>?> GetEligibleTeacherDtosAsync(int schoolYearId, CancellationToken cancellationToken = default) => Task.FromResult<List<TeacherOptionDto>?>(null);
    Task<PaginatedResult<RosterStudentItemDto>?> GetEligibleStudentsPagedAsync(int schoolYearId, string? search, int pageIndex, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<PaginatedResult<RosterStudentItemDto>?>(null);
    Task<ClassRosterDto?> GetClassRosterDtoAsync(int classId, CancellationToken cancellationToken = default) => Task.FromResult<ClassRosterDto?>(null);
    Task<(ClassRosterDto Roster, int? HomeroomTeacherUserId)?> GetClassRosterWithAccessCheckAsync(int classId, CancellationToken cancellationToken = default) => Task.FromResult<(ClassRosterDto, int?)?>(null);
    Task<int?> GetHomeroomClassIdByTeacherUserIdAsync(int teacherUserId, int? schoolYearId = null, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
}
