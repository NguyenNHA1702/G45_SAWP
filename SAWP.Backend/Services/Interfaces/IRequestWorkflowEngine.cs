using Core.DTOs;

namespace Services.Interfaces;

public interface IRequestWorkflowEngine
{
    // Parent operations
    Task<ApiResponse<string>> SubmitClassTransferRequestAsync(int requesterUserId, ClassTransferRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<string>> SubmitDocumentRequestAsync(int requesterUserId, DocumentIssuanceRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>> GetMyRequestsAsync(int parentUserId, int? studentId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<AdministrativeRequestSummaryDto>> GetRequestDetailAsync(int requestId, int currentUserId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ResubmitRequestAsync(int requestId, int requesterUserId, ResubmitRequestDto request, CancellationToken cancellationToken = default);

    // Office Staff operations
    Task<ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>> GetOfficeStaffRequestsAsync(string? requestType = null, string? status = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> RequestAmendmentAsync(int requestId, int staffUserId, StaffAmendmentDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> EndorseTransferAsync(int requestId, int staffUserId, StaffEndorseTransferDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> EndorseDocumentAsync(int requestId, int staffUserId, StaffEndorseDocumentDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ExecuteClassTransferAsync(int requestId, int staffUserId, CancellationToken cancellationToken = default);

    // School Board operations
    Task<ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>> GetSchoolBoardRequestsAsync(string? requestType = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ProcessBoardDecisionAsync(int requestId, int boardUserId, BoardDecisionDto request, CancellationToken cancellationToken = default);

    // Legacy fallback
    Task<ApiResponse<bool>> ProcessRequestAsync(int requestId, int reviewerUserId, ProcessRequestDto request, CancellationToken cancellationToken = default);
}
