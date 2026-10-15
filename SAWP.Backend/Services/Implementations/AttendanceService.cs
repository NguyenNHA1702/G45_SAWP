using Core.DTOs;
using Core.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

public class AttendanceService : IAttendanceService
{
    private readonly IUnitOfWork _unitOfWork;

    public AttendanceService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> RecordDailyAttendanceAsync(int classId, DateTime date, List<DailyAttendanceEntryDto> entries, int recordedByUserId, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> SubmitLeaveRequestAsync(int requesterUserId, LeaveRequestSubmitDto request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<bool>.Ok(true);
    }
}
