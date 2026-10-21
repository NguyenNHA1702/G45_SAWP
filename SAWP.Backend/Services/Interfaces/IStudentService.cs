using Core.DTOs;

namespace Services.Interfaces;

public interface IStudentService
{
    Task<ApiResponse<StudentProfileDto>> GetStudentProfileAsync(int studentId, int? requestingUserId = null, string? requestingUserRole = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginatedResult<StudentProfileDto>>> GetStudentsPagedAsync(StudentFilterDto filter, CancellationToken cancellationToken = default);
    Task<ApiResponse<StudentProfileDto>> CreateStudentAsync(CreateStudentDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<StudentProfileDto>> UpdateStudentAsync(int studentId, UpdateStudentDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<DuplicateCheckResultDto>> CheckDuplicateAsync(string studentCode, CancellationToken cancellationToken = default);
    Task<ApiResponse<StudentProfileDto>> ResolveDuplicateAsync(ResolveDuplicateStudentDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ActivateStudentAsync(int studentId, int? actorUserId = null, CancellationToken cancellationToken = default);
}
