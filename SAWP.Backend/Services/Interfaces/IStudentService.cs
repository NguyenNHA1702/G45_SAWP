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

    // UC-10 Link or Unlink Guardian to Student (Office Staff)
    Task<ApiResponse<List<GuardianRecordDto>>> SearchGuardianRecordsAsync(string? search, CancellationToken ct = default);
    Task<ApiResponse<StudentProfileDto>> LinkGuardianAsync(int studentId, LinkGuardianRequestDto request, int? actorUserId = null, CancellationToken ct = default);
    Task<ApiResponse<StudentProfileDto>> UpdateGuardianLinkAsync(int studentId, int guardianId, UpdateGuardianLinkRequestDto request, int? actorUserId = null, CancellationToken ct = default);
    Task<ApiResponse<StudentProfileDto>> UnlinkGuardianAsync(int studentId, int guardianId, int? actorUserId = null, CancellationToken ct = default);
}
