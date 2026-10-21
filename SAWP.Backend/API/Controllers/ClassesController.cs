using System.Security.Claims;
using API.Extensions;
using Core.Constants;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClassesController : ControllerBase
{
    private readonly IClassService _classService;

    public ClassesController(IClassService classService)
    {
        _classService = classService;
    }

    #region Academic Year (UC-13)

    [HttpPost("school-years")]
    [Authorize(Roles = "OfficeStaff,Admin,SchoolBoard")]
    [ProducesResponseType(typeof(ApiResponse<SchoolYearDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SchoolYearDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSchoolYear([FromBody] CreateSchoolYearDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.CreateSchoolYearAsync(request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("school-years")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<SchoolYearDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchoolYears(CancellationToken cancellationToken)
    {
        var result = await _classService.GetSchoolYearsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("school-years/active")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SchoolYearDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SchoolYearDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveSchoolYear(CancellationToken cancellationToken)
    {
        var result = await _classService.GetActiveSchoolYearAsync(cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("school-years/{id}/activate")]
    [Authorize(Roles = "OfficeStaff,Admin,SchoolBoard")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateSchoolYear(int id, CancellationToken cancellationToken)
    {
        var result = await _classService.ActivateSchoolYearAsync(id, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    #endregion

    #region Classes Management (UC-14)

    [HttpPost]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.CreateClassAsync(request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("initialize-batch")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<ClassSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ClassSummaryDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitializeBatchClasses([FromBody] InitializeBatchClassesDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.InitializeBatchClassesAsync(request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<ClassSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClasses(
        [FromQuery] int? schoolYearId,
        [FromQuery] int? grade,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _classService.GetClassesAsync(schoolYearId, grade, status, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassDetails(int id, CancellationToken cancellationToken)
    {
        var result = await _classService.GetClassDetailsAsync(id, cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateClass(int id, [FromBody] UpdateClassDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.UpdateClassAsync(id, request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    #endregion

    #region Homeroom Teacher Assignment (UC-15)

    [HttpGet("eligible-teachers")]
    [Authorize(Roles = "OfficeStaff,Admin,SchoolBoard")]
    [ProducesResponseType(typeof(ApiResponse<List<TeacherOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleTeachers([FromQuery] int? schoolYearId, CancellationToken cancellationToken)
    {
        var result = await _classService.GetEligibleTeachersAsync(schoolYearId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/assign-homeroom-teacher")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignHomeroomTeacher(int id, [FromBody] AssignTeacherDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.AssignHomeroomTeacherAsync(id, request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    #endregion

    #region Student Placement & Class Assignment (UC-16)

    [HttpGet("{id}/eligible-students")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<RosterStudentItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleStudents(int id, CancellationToken cancellationToken)
    {
        var result = await _classService.GetEligibleStudentsForAssignmentAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/assign-students")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignStudents(int id, [FromBody] AssignStudentsDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.AssignStudentsToClassAsync(id, request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}/remove-student/{studentId}")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveStudent(int id, int studentId, CancellationToken cancellationToken)
    {
        var result = await _classService.RemoveStudentFromClassAsync(id, studentId, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    #endregion

    #region Placement Publishing (Workflow Step 6)

    [HttpPost("{id}/publish-placement")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PublishPlacementResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PublishPlacementResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PublishPlacement(int id, CancellationToken cancellationToken)
    {
        var result = await _classService.PublishPlacementAsync(id, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("publish-all-placements")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<PublishPlacementResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishAllPlacements([FromQuery] int schoolYearId, CancellationToken cancellationToken)
    {
        var result = await _classService.PublishAllPlacementsAsync(schoolYearId, User.GetUserId(), cancellationToken);
        return Ok(result);
    }

    #endregion

    #region Class Roster (UC-17)

    [HttpGet("{id}/roster")]
    [Authorize(Roles = "Teacher,OfficeStaff,SchoolBoard,Admin")]
    [ProducesResponseType(typeof(ApiResponse<ClassRosterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassRosterDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ClassRosterDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassRoster(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        var result = await _classService.GetClassRosterAsync(id, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            if (result.Code == SystemMessages.MSG10.Code || result.Code == SystemMessages.MSG20.Code)
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpGet("my-homeroom-class")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(ApiResponse<ClassRosterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassRosterDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyHomeroomClass(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<ClassRosterDto>.Fail(SystemMessages.MSG07));
        }

        var result = await _classService.GetMyHomeroomClassAsync(userId.Value, cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    #endregion
}
