using Core.DTOs;

namespace Services.Interfaces;

public interface IStudentService
{
    Task<ApiResponse<StudentProfileDto>> GetStudentProfileAsync(int studentId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginatedResult<StudentProfileDto>>> GetStudentsPagedAsync(int pageIndex, int pageSize, int? classId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<StudentProfileDto>> CreateStudentAsync(CreateStudentDto request, CancellationToken cancellationToken = default);
}
