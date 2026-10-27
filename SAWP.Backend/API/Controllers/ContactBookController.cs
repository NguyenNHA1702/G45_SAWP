using API.Extensions;
using Core.Constants;
using Core.DTOs;
using Core.DTOs.ContactBook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/contact-book")]
[Authorize]
public class ContactBookController : BaseApiController
{
    private readonly IEContactBookService _contactBookService;

    public ContactBookController(IEContactBookService contactBookService)
    {
        _contactBookService = contactBookService;
    }

    #region 1. Bảng tin lớp học (UC-61 & UC-62)

    /// <summary>
    /// Lấy danh sách bài đăng bảng tin lớp học
    /// </summary>
    [HttpGet("classes/{classId}/posts")]
    public async Task<IActionResult> GetClassPosts(int classId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.GetClassPostsAsync(userId.Value, classId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// GVCN đăng bài viết mới lên bảng tin lớp (UC-61)
    /// </summary>
    [HttpPost("posts")]
    public async Task<IActionResult> CreateClassPost([FromBody] CreateClassPostDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.CreateClassPostAsync(userId.Value, request, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Ghim hoặc gỡ ghim bài viết lớp học
    /// </summary>
    [HttpPost("posts/{postId}/pin")]
    public async Task<IActionResult> TogglePinPost(int postId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.TogglePinPostAsync(userId.Value, postId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Xóa bài viết lớp học
    /// </summary>
    [HttpDelete("posts/{postId}")]
    public async Task<IActionResult> DeleteClassPost(int postId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.DeleteClassPostAsync(userId.Value, postId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Thêm bình luận tương tác vào bài viết lớp (UC-62)
    /// </summary>
    [HttpPost("posts/{postId}/comments")]
    public async Task<IActionResult> AddComment(int postId, [FromBody] CreatePostCommentDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.AddCommentAsync(userId.Value, postId, request, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Xóa bình luận bài viết
    /// </summary>
    [HttpDelete("comments/{commentId}")]
    public async Task<IActionResult> DeleteComment(int commentId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.DeleteCommentAsync(userId.Value, commentId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Thả cảm xúc / like bài viết
    /// </summary>
    [HttpPost("posts/{postId}/reactions")]
    public async Task<IActionResult> ToggleReaction(int postId, [FromBody] ToggleReactionDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.ToggleReactionAsync(userId.Value, postId, request.ReactionType, cancellationToken);
        return ToActionResult(result);
    }

    #endregion

    #region 2. Sổ nhận xét học sinh & Ký nhận điện tử (UC-63 & UC-64)

    /// <summary>
    /// GVCN lấy danh sách phiếu nhận xét của cả lớp theo tuần (UC-63)
    /// </summary>
    [HttpGet("classes/{classId}/evaluations/roster")]
    public async Task<IActionResult> GetClassEvaluationsRoster(int classId, [FromQuery] int weekNumber = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.GetClassEvaluationsRosterAsync(userId.Value, classId, weekNumber, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Lấy lịch sử phiếu nhận xét của một học sinh cụ thể (UC-64)
    /// </summary>
    [HttpGet("students/{studentId}/evaluations")]
    public async Task<IActionResult> GetStudentEvaluations(int studentId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.GetStudentEvaluationsAsync(userId.Value, studentId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// GVCN lưu hoặc cập nhật phiếu nhận xét rèn luyện nề nếp tuần (UC-63)
    /// </summary>
    [HttpPost("evaluations")]
    public async Task<IActionResult> CreateOrUpdateEvaluation([FromBody] CreateStudentEvaluationDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.CreateOrUpdateEvaluationAsync(userId.Value, request, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Phụ huynh ký nhận "Đã đọc" và gửi phản hồi 2 chiều cho GVCN (UC-64)
    /// </summary>
    [HttpPost("evaluations/{id}/acknowledge")]
    public async Task<IActionResult> AcknowledgeEvaluation(int id, [FromBody] AcknowledgeEvaluationDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _contactBookService.AcknowledgeEvaluationAsync(userId.Value, id, request, cancellationToken);
        return ToActionResult(result);
    }

    #endregion
}
