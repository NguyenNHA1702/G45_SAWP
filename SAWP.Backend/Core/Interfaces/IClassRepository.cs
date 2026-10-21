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
}
