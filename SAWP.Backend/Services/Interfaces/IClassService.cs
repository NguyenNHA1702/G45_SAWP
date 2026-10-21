using Core.DTOs;

namespace Services.Interfaces;

public interface IClassService
{
    // Academic Year
    Task<ApiResponse<SchoolYearDto>> CreateSchoolYearAsync(CreateSchoolYearDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<SchoolYearDto>>> GetSchoolYearsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<SchoolYearDto>> GetActiveSchoolYearAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ActivateSchoolYearAsync(int schoolYearId, int? actorUserId = null, CancellationToken cancellationToken = default);

    // Classes
    Task<ApiResponse<ClassSummaryDto>> CreateClassAsync(CreateClassDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<ClassSummaryDto>>> InitializeBatchClassesAsync(InitializeBatchClassesDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<ClassSummaryDto>>> GetClassesAsync(int? schoolYearId = null, int? grade = null, string? status = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<ClassSummaryDto>> GetClassDetailsAsync(int classId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ClassSummaryDto>> UpdateClassAsync(int classId, UpdateClassDto request, int? actorUserId = null, CancellationToken cancellationToken = default);

    // Homeroom Teacher
    Task<ApiResponse<List<TeacherOptionDto>>> GetEligibleTeachersAsync(int? schoolYearId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AssignHomeroomTeacherAsync(int classId, AssignTeacherDto request, int? actorUserId = null, CancellationToken cancellationToken = default);

    // Student Placement / Class Assignment
    Task<ApiResponse<List<RosterStudentItemDto>>> GetEligibleStudentsForAssignmentAsync(int classId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AssignStudentsToClassAsync(int classId, AssignStudentsDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> RemoveStudentFromClassAsync(int classId, int studentId, int? actorUserId = null, CancellationToken cancellationToken = default);

    // Placement Publishing
    Task<ApiResponse<PublishPlacementResultDto>> PublishPlacementAsync(int classId, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<PublishPlacementResultDto>>> PublishAllPlacementsAsync(int schoolYearId, int? actorUserId = null, CancellationToken cancellationToken = default);

    // Class Roster
    Task<ApiResponse<ClassRosterDto>> GetClassRosterAsync(int classId, int? requestingUserId = null, string? requestingUserRole = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<ClassRosterDto>> GetMyHomeroomClassAsync(int teacherUserId, CancellationToken cancellationToken = default);
}
