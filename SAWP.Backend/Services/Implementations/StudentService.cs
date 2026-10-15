using Core.DTOs;
using Core.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

public class StudentService : IStudentService
{
    private readonly IUnitOfWork _unitOfWork;

    public StudentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<StudentProfileDto>> GetStudentProfileAsync(int studentId, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<StudentProfileDto>.Ok(new StudentProfileDto { Id = studentId });
    }

    public async Task<ApiResponse<PaginatedResult<StudentProfileDto>>> GetStudentsPagedAsync(int pageIndex, int pageSize, int? classId = null, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<PaginatedResult<StudentProfileDto>>.Ok(new PaginatedResult<StudentProfileDto>());
    }

    public async Task<ApiResponse<StudentProfileDto>> CreateStudentAsync(CreateStudentDto request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<StudentProfileDto>.Ok(new StudentProfileDto { StudentCode = request.StudentCode, FullName = request.FullName });
    }
}
