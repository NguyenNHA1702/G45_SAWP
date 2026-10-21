using Core.DTOs;

namespace Services.Interfaces;

public interface IGuardianService
{
    // Parent submits link request (Step 8 & 9)
    Task<ApiResponse<ChildLinkRequestResultDto>> SubmitChildLinkRequestAsync(int parentUserId, SubmitChildLinkDto request, CancellationToken cancellationToken = default);

    // Office Staff reviews & manually verifies requests (Step 9)
    Task<ApiResponse<List<ChildLinkRequestItemDto>>> GetLinkRequestsAsync(string? status = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> VerifyChildLinkRequestAsync(int requestId, int reviewerUserId, VerifyChildLinkDto review, CancellationToken cancellationToken = default);

    // Parent Portal Dashboard & Class Information (Step 10)
    Task<ApiResponse<List<LinkedChildSummaryDto>>> GetMyLinkedChildrenAsync(int parentUserId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ChildDashboardDto>> GetChildDashboardAsync(int parentUserId, int studentId, CancellationToken cancellationToken = default);
}
