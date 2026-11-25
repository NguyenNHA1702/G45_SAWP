using Core.DTOs;

namespace Services.Interfaces;

public interface IPlacementPlanService
{
    Task<ApiResponse<PlacementPlanDetailDto>> GenerateDraftPlanAsync(
        GeneratePlacementPlanRequestDto request,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PlacementPlanDetailDto>> GetPlanByIdAsync(
        int planId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<PlacementPlanSummaryDto>>> GetPlanHistoryAsync(
        int? schoolYearId = null,
        int? grade = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PaginatedResult<PlacementPlanSummaryDto>>> GetPlanHistoryPagedAsync(
        int? schoolYearId = null,
        int? grade = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PlacementPlanDetailDto>> MoveStudentAsync(
        int planId,
        MoveStudentInPlanDto request,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PlacementPlanDetailDto>> SwapStudentsAsync(
        int planId,
        SwapStudentsInPlanDto request,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PlacementPlanSummaryDto>> SubmitPlanAsync(
        int planId,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PlacementPlanSummaryDto>> ReviewPlanAsync(
        int planId,
        ReviewPlacementPlanDto request,
        int reviewerUserId,
        CancellationToken cancellationToken = default);
}
