using API.Extensions;
using Core.DTOs;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParentsController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public ParentsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Lấy danh sách học sinh (con) của phụ huynh đang đăng nhập
    /// </summary>
    [HttpGet("my-children")]
    [Authorize(Roles = "Parent,Admin")]
    public async Task<IActionResult> GetMyChildren(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var children = await _unitOfWork.Students.GetChildrenByGuardianUserIdAsync(userId, ct);
        return Ok(ApiResponse<IReadOnlyList<ChildSummaryDto>>.Ok(children));
    }
}
