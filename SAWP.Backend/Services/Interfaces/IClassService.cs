using Core.DTOs;

namespace Services.Interfaces;

public interface IClassService
{
    // Academic Year
    Task<ApiResponse<SchoolYearDto>> CreateSchoolYearAsync(CreateSchoolYearDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<SchoolYearDto>>> GetSchoolYearsAsync(bool includeDraft = true, CancellationToken cancellationToken = default);
    Task<ApiResponse<SchoolYearDto>> GetActiveSchoolYearAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ActivateSchoolYearAsync(int schoolYearId, int? actorUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Marks a class of a Draft school year as Reviewed (UC-14 A2, BR-72).</summary>
    Task<ApiResponse<ClassSummaryDto>> MarkClassReviewedAsync(int classId, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteSchoolYearAsync(int schoolYearId, int? actorUserId = null, CancellationToken cancellationToken = default, bool force = false);

    // Classes
    Task<ApiResponse<ClassSummaryDto>> CreateClassAsync(CreateClassDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<ClassSummaryDto>>> GetClassesAsync(int? schoolYearId = null, int? grade = null, string? status = null, bool includeDraft = true, CancellationToken cancellationToken = default);
    Task<ApiResponse<ClassSummaryDto>> GetClassDetailsAsync(int classId, bool includeDraft = true, CancellationToken cancellationToken = default);
    Task<ApiResponse<ClassSummaryDto>> UpdateClassAsync(int classId, UpdateClassDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteClassAsync(int classId, int? actorUserId = null, CancellationToken cancellationToken = default);

    // Homeroom Teacher
    Task<ApiResponse<List<TeacherOptionDto>>> GetEligibleTeachersAsync(int? schoolYearId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AssignHomeroomTeacherAsync(int classId, AssignTeacherDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UnassignHomeroomTeacherAsync(int classId, int? actorUserId = null, CancellationToken cancellationToken = default);

    // Student Class Assignment
    Task<ApiResponse<List<RosterStudentItemDto>>> GetEligibleStudentsForAssignmentAsync(int classId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginatedResult<RosterStudentItemDto>>> GetEligibleStudentsForAssignmentPagedAsync(int classId, string? search = null, int pageIndex = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AssignStudentsToClassAsync(int classId, AssignStudentsDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> RemoveStudentFromClassAsync(int classId, int studentId, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> RemoveStudentsBatchFromClassAsync(int classId, List<int> studentIds, int? actorUserId = null, CancellationToken cancellationToken = default);


    // Class Roster
    Task<ApiResponse<ClassRosterDto>> GetClassRosterAsync(int classId, int? requestingUserId = null, string? requestingUserRole = null, bool includeDraft = true, CancellationToken cancellationToken = default);
    Task<ApiResponse<ClassRosterDto>> GetMyHomeroomClassAsync(int teacherUserId, CancellationToken cancellationToken = default);
}
