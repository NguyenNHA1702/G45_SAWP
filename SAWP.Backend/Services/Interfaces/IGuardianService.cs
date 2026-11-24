using Core.DTOs;

namespace Services.Interfaces;

public interface IGuardianService
{
    // Parent Portal Dashboard & Class Information (Step 10)
    Task<ApiResponse<List<LinkedChildSummaryDto>>> GetMyLinkedChildrenAsync(int parentUserId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ChildDashboardDto>> GetChildDashboardAsync(int parentUserId, int studentId, CancellationToken cancellationToken = default);
}
