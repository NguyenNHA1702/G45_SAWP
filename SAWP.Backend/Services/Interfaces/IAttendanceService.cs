using Core.DTOs;

namespace Services.Interfaces;

public interface IAttendanceService
{
    Task<ApiResponse<bool>> RecordDailyAttendanceAsync(int classId, DateTime date, List<DailyAttendanceEntryDto> entries, int recordedByUserId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> SubmitLeaveRequestAsync(int requesterUserId, LeaveRequestSubmitDto request, CancellationToken cancellationToken = default);
}
