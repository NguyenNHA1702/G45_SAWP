using Core.DTOs;

namespace Services.Interfaces;

/// <summary>UC-67 Request Parent Accounts (Office Staff) and UC-68 Review Account Requests (System Administrator).</summary>
public interface IAccountRequestService
{
    // Office Staff (UC-67)
    Task<ApiResponse<List<GuardianWithoutAccountDto>>> GetGuardiansWithoutAccountAsync(string? search, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResult<GuardianWithoutAccountDto>>> GetGuardiansWithoutAccountPagedAsync(string? search, int pageIndex = 1, int pageSize = 25, CancellationToken ct = default);
    Task<ApiResponse<List<AccountRequestRowCheckDto>>> CheckRowsAsync(List<int> guardianIds, CancellationToken ct = default);
    Task<ApiResponse<SaveAccountRequestResultDto>> CreateRequestAsync(int userId, SaveAccountRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<SaveAccountRequestResultDto>> UpdateDraftAsync(int userId, int requestId, SaveAccountRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<List<AccountRequestDto>>> GetMyRequestsAsync(int userId, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResult<AccountRequestDto>>> GetMyRequestsPagedAsync(int userId, int pageIndex = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ApiResponse<AccountRequestDto>> GetRequestAsync(int userId, bool isAdministrator, int requestId, CancellationToken ct = default);
    Task<ApiResponse<AccountRequestDto>> CloseRowsAsync(int userId, int requestId, AccountRequestRowsDto request, CancellationToken ct = default);
    Task<ApiResponse<SaveAccountRequestResultDto>> ResubmitRowsAsync(int userId, int requestId, AccountRequestRowsDto request, CancellationToken ct = default);
    Task<ApiResponse<AccountRequestItemDto>> ResendActivationAsync(int userId, int itemId, ResendActivationDto request, CancellationToken ct = default);

    // System Administrator (UC-68)
    Task<ApiResponse<List<AccountRequestDto>>> GetRequestsForReviewAsync(string? status, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResult<AccountRequestDto>>> GetRequestsForReviewPagedAsync(string? status, string? search = null, int pageIndex = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ApiResponse<AccountRequestDecisionResultDto>> DecideAsync(int adminUserId, int requestId, AccountRequestDecisionDto decision, CancellationToken ct = default);

    // Parent Email Change Requests (Office Staff -> Admin review, BR-69)
    Task<ApiResponse<PaginatedResult<GuardianWithAccountDto>>> GetGuardiansWithAccountPagedAsync(string? search, int pageIndex = 1, int pageSize = 25, CancellationToken ct = default);
    Task<ApiResponse<EmailChangeRequestDto>> CreateEmailChangeRequestAsync(int staffUserId, CreateEmailChangeRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResult<EmailChangeRequestDto>>> GetMyEmailChangeRequestsPagedAsync(int staffUserId, int pageIndex = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResult<EmailChangeRequestDto>>> GetEmailChangeRequestsForReviewPagedAsync(string? status, string? search = null, int pageIndex = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ApiResponse<EmailChangeRequestDto>> DecideEmailChangeRequestAsync(int adminUserId, int requestId, DecideEmailChangeRequestDto decision, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResendEmailChangeLinkAsync(int actorUserId, int requestId, CancellationToken ct = default);
}
