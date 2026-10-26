using API.Extensions;
using Core.Constants;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/placement-plans")]
public class PlacementPlansController : ControllerBase
{
    private readonly IPlacementPlanService _placementPlanService;

    public PlacementPlansController(IPlacementPlanService placementPlanService)
    {
        _placementPlanService = placementPlanService;
    }

    /// <summary>
    /// Step 1 & 2: Giáo vụ nhập tiêu chí và hệ thống sinh phương án nháp
    /// </summary>
    [HttpPost("generate")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanDetailDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateDraftPlan([FromBody] GeneratePlacementPlanRequestDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG07));
        }

        var result = await _placementPlanService.GenerateDraftPlanAsync(request, userId.Value, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Step 3: Xem trước kết quả xếp lớp (Draft Placement Preview) và các tiêu chí vi phạm / cảnh báo
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OfficeStaff,SchoolBoard,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanById(int id, CancellationToken cancellationToken)
    {
        var result = await _placementPlanService.GetPlanByIdAsync(id, cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Lịch sử các phương án xếp lớp theo năm học và khối
    /// </summary>
    [HttpGet("history")]
    [Authorize(Roles = "OfficeStaff,SchoolBoard,Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<PlacementPlanSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlanHistory(
        [FromQuery] int? schoolYearId,
        [FromQuery] int? grade,
        CancellationToken cancellationToken)
    {
        var result = await _placementPlanService.GetPlanHistoryAsync(schoolYearId, grade, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Giáo vụ đổi chỗ / di chuyển học sinh thủ công trong phương án nháp
    /// </summary>
    [HttpPost("{id}/move-student")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanDetailDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MoveStudent(int id, [FromBody] MoveStudentInPlanDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG07));
        }

        var result = await _placementPlanService.MoveStudentAsync(id, request, userId.Value, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Giáo vụ đổi chỗ hai học sinh giữa hai lớp
    /// </summary>
    [HttpPost("{id}/swap-students")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanDetailDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SwapStudents(int id, [FromBody] SwapStudentsInPlanDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG07));
        }

        var result = await _placementPlanService.SwapStudentsAsync(id, request, userId.Value, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Giáo vụ trình phương án lên Ban Giám hiệu / Hiệu trưởng phê duyệt
    /// </summary>
    [HttpPost("{id}/submit")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitPlan(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<PlacementPlanSummaryDto>.Fail(SystemMessages.MSG07));
        }

        var result = await _placementPlanService.SubmitPlanAsync(id, userId.Value, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Step 5 & 6: Hiệu trưởng phê duyệt phương án hoặc trả lại yêu cầu điều chỉnh.
    /// Chỉ sau phê duyệt mới xác lập lớp chính thức và tạo danh sách cho giáo viên.
    /// </summary>
    [HttpPost("{id}/review")]
    [Authorize(Roles = "SchoolBoard,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlacementPlanSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReviewPlan(int id, [FromBody] ReviewPlacementPlanDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<PlacementPlanSummaryDto>.Fail(SystemMessages.MSG07));
        }

        var result = await _placementPlanService.ReviewPlanAsync(id, request, userId.Value, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
