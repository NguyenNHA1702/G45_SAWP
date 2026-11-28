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
    private const long MaxImportFileBytes = 5 * 1024 * 1024;

    private readonly IClassService _classService;
    private readonly IClassImporter _classImporter;

    public ClassesController(IClassService classService, IClassImporter classImporter)
    {
        _classService = classService;
        _classImporter = classImporter;
    }

    private bool CanViewDraftSchoolYears()
        => User.IsInRole("OfficeStaff") || User.IsInRole("SchoolBoard");

    #region Academic Year (UC-13)

    [HttpPost("school-years")]
    [Authorize(Roles = "OfficeStaff")]
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
        var result = await _classService.GetSchoolYearsAsync(CanViewDraftSchoolYears(), cancellationToken);
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
    [Authorize(Roles = "OfficeStaff")] // BR-72: Office Staff activate a Draft school year
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateSchoolYear(int id, CancellationToken cancellationToken)
    {
        var result = await _classService.ActivateSchoolYearAsync(id, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("school-years/{id}")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSchoolYear(int id, [FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        var result = await _classService.DeleteSchoolYearAsync(id, User.GetUserId(), cancellationToken, force);
        if (result.Success) return Ok(result);
        if (result.Errors != null && result.Errors.Any(e => e.Contains("không tồn tại")))
            return NotFound(result);
        return result.Code == SystemMessages.MSG09.Code ? Conflict(result) : BadRequest(result);
    }

    #endregion

    #region Import Class Assignments (UC-69)

    /// <summary>Reads the uploaded .xlsx file and previews the import into the Draft school year (steps 1-3).</summary>
    [HttpPost("school-years/{id}/import/preview")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<ClassImportPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassImportPreviewDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewClassImport(int id, IFormFile? file, [FromForm] string? mode, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0 || file.Length > MaxImportFileBytes
            || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<ClassImportPreviewDto>.Fail(SystemMessages.MSG41, new List<string> { "Chỉ hỗ trợ tệp Excel (.xlsx) không quá 5 MB." }));
        }

        using var stream = file.OpenReadStream();
        var parsed = _classImporter.ParseWorkbook(stream);
        if (!parsed.Success || parsed.Data == null)
        {
            return BadRequest(ApiResponse<ClassImportPreviewDto>.Fail(SystemMessages.MSG41, parsed.Errors));
        }

        parsed.Data.Mode = mode ?? ClassImportModes.Merge;
        var result = await _classImporter.PreviewAsync(id, parsed.Data, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Previews already parsed rows again, e.g. after choosing Replace or Merge (A2).</summary>
    [HttpPost("school-years/{id}/import/preview-rows")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<ClassImportPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassImportPreviewDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewClassImportRows(int id, [FromBody] ClassImportRowsDto request, CancellationToken cancellationToken)
    {
        var result = await _classImporter.PreviewAsync(id, request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Imports the valid rows (steps 4-6); the rows are validated again.</summary>
    [HttpPost("school-years/{id}/import/confirm")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<ClassImportResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassImportResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmClassImport(int id, [FromBody] ClassImportRowsDto request, CancellationToken cancellationToken)
    {
        var result = await _classImporter.ImportAsync(id, request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("import/template")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public IActionResult DownloadClassImportTemplate()
        => File(_classImporter.GenerateTemplate(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_Xep_Lop_SAWP.xlsx");

    #endregion

    #region Classes Management (UC-14)

    [HttpPost]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.CreateClassAsync(request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    [Authorize(Roles = "OfficeStaff,SchoolBoard,Teacher")]
    [ProducesResponseType(typeof(ApiResponse<List<ClassSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClasses(
        [FromQuery] int? schoolYearId,
        [FromQuery] int? grade,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _classService.GetClassesAsync(schoolYearId, grade, status, CanViewDraftSchoolYears(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "OfficeStaff,SchoolBoard,Teacher")]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassDetails(int id, CancellationToken cancellationToken)
    {
        var result = await _classService.GetClassDetailsAsync(id, CanViewDraftSchoolYears(), cancellationToken);
        if (result.Success) return Ok(result);
        return result.Code == SystemMessages.MSG10.Code
            ? StatusCode(StatusCodes.Status403Forbidden, result)
            : NotFound(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateClass(int id, [FromBody] UpdateClassDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.UpdateClassAsync(id, request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Marks a class of a Draft school year as Reviewed (UC-14 A2, BR-72).</summary>
    [HttpPost("{id}/mark-reviewed")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkClassReviewed(int id, CancellationToken cancellationToken)
    {
        var result = await _classService.MarkClassReviewedAsync(id, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteClass(int id, CancellationToken cancellationToken)
    {
        var result = await _classService.DeleteClassAsync(id, User.GetUserId(), cancellationToken);
        if (result.Success) return Ok(result);
        if (result.Errors != null && result.Errors.Any(e => e.Contains("không tồn tại")))
            return NotFound(result);
        return BadRequest(result);
    }

    #endregion

    #region Homeroom Teacher Assignment (UC-15)

    [HttpGet("eligible-teachers")]
    [Authorize(Roles = "OfficeStaff,SchoolBoard")]
    [ProducesResponseType(typeof(ApiResponse<List<TeacherOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleTeachers([FromQuery] int? schoolYearId, CancellationToken cancellationToken)
    {
        var result = await _classService.GetEligibleTeachersAsync(schoolYearId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/assign-homeroom-teacher")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignHomeroomTeacher(int id, [FromBody] AssignTeacherDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.AssignHomeroomTeacherAsync(id, request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/unassign-homeroom-teacher")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnassignHomeroomTeacher(int id, CancellationToken cancellationToken)
    {
        var result = await _classService.UnassignHomeroomTeacherAsync(id, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    #endregion

    #region Student Class Assignment (UC-16)

    [HttpGet("{id}/eligible-students/paged")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<RosterStudentItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleStudentsPaged(
        int id,
        [FromQuery] string? search,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _classService.GetEligibleStudentsForAssignmentPagedAsync(id, search, pageIndex, pageSize, cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{id}/assign-students")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignStudents(int id, [FromBody] AssignStudentsDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.AssignStudentsToClassAsync(id, request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}/remove-student/{studentId}")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveStudent(int id, int studentId, CancellationToken cancellationToken)
    {
        var result = await _classService.RemoveStudentFromClassAsync(id, studentId, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/remove-students-batch")]
    [Authorize(Roles = "OfficeStaff")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveStudentsBatch(int id, [FromBody] RemoveStudentsBatchDto request, CancellationToken cancellationToken)
    {
        var result = await _classService.RemoveStudentsBatchFromClassAsync(id, request.StudentIds, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    #endregion

    #region Class Roster (UC-17)

    [HttpGet("{id}/roster")]
    [Authorize(Roles = "Teacher,OfficeStaff,SchoolBoard")]
    [ProducesResponseType(typeof(ApiResponse<ClassRosterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassRosterDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ClassRosterDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassRoster(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        var result = await _classService.GetClassRosterAsync(id, userId, userRole, CanViewDraftSchoolYears(), cancellationToken);
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
