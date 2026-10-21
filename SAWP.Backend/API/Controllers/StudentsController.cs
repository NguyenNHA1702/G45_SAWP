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
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IStudentImporter _studentImporter;

    public StudentsController(IStudentService studentService, IStudentImporter studentImporter)
    {
        _studentService = studentService;
        _studentImporter = studentImporter;
    }

    [HttpPost]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto request, CancellationToken cancellationToken)
    {
        var result = await _studentService.CreateStudentAsync(request, User.GetUserId(), cancellationToken);
        if (!result.Success)
        {
            if (result.Code == SystemMessages.MSG12.Code)
            {
                return Conflict(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<StudentProfileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudents([FromQuery] StudentFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _studentService.GetStudentsPagedAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentProfile(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        var result = await _studentService.GetStudentProfileAsync(id, userId, userRole, cancellationToken);
        if (!result.Success)
        {
            if (result.Code == SystemMessages.MSG10.Code)
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStudent(int id, [FromBody] UpdateStudentDto request, CancellationToken cancellationToken)
    {
        var result = await _studentService.UpdateStudentAsync(id, request, User.GetUserId(), cancellationToken);
        if (!result.Success)
        {
            if (result.Code == SystemMessages.MSG11.Code)
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id}/activate")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateStudent(int id, CancellationToken cancellationToken)
    {
        var result = await _studentService.ActivateStudentAsync(id, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("check-duplicate")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DuplicateCheckResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DuplicateCheckResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckDuplicate([FromQuery] string studentCode, CancellationToken cancellationToken)
    {
        var result = await _studentService.CheckDuplicateAsync(studentCode, cancellationToken);
        return Ok(result);
    }

    [HttpPost("resolve-duplicate")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResolveDuplicate([FromBody] ResolveDuplicateStudentDto request, CancellationToken cancellationToken)
    {
        var result = await _studentService.ResolveDuplicateAsync(request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    #region Excel Bulk Import (UC-09)

    [HttpPost("import/parse")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<ImportStudentRowDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ImportStudentRowDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseExcel(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<List<ImportStudentRowDto>>.Fail(SystemMessages.MSG41, new List<string> { "Vui lòng chọn tệp Excel (.xlsx)." }));
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<List<ImportStudentRowDto>>.Fail(SystemMessages.MSG41, new List<string> { "Chỉ hỗ trợ tệp định dạng .xlsx." }));
        }

        using var stream = file.OpenReadStream();
        var result = await _studentImporter.ParseExcelAsync(stream, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("import/confirm")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    [ProducesResponseType(typeof(ApiResponse<ImportStudentsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ImportStudentsResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmImport([FromBody] ImportStudentsConfirmDto request, CancellationToken cancellationToken)
    {
        var result = await _studentImporter.ImportStudentsAsync(request, User.GetUserId(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("import/template")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var bytes = await _studentImporter.GenerateExcelTemplateAsync(cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_Danh_Sach_Hoc_Sinh_SAWP.xlsx");
    }

    #endregion
}
