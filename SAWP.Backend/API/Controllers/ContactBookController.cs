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
    /// Tải lên tệp đính kèm cho bài viết bảng tin lớp (hình ảnh hoặc tài liệu) (UC-61, BR-50)
    /// </summary>
    [HttpPost("upload-attachment")]
    public async Task<IActionResult> UploadAttachment(
        IFormFile file,
        [FromServices] Infrastructure.ExternalServices.IFileStorageService fileStorageService,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<ContactAttachmentUploadResultDto>.Fail(SystemMessages.MSG01));
        }

        // BR-50: Giới hạn kích thước tệp tối đa 5MB
        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<ContactAttachmentUploadResultDto>.Fail(SystemMessages.MSG41));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse<ContactAttachmentUploadResultDto>.Fail(SystemMessages.MSG41));
        }

        var isImg = ext is ".jpg" or ".jpeg" or ".png" or ".webp";
        using var stream = file.OpenReadStream();
        var relativePath = await fileStorageService.SaveFileAsync(stream, file.FileName, "contact-book", cancellationToken);
        var normalizedPath = relativePath.Replace('\\', '/');
        var fileUrl = $"/api/contact-book/attachment-file?path={Uri.EscapeDataString(normalizedPath)}";

        return Ok(ApiResponse<ContactAttachmentUploadResultDto>.Ok(new ContactAttachmentUploadResultDto
        {
            Url = fileUrl,
            FileName = file.FileName,
            FileType = isImg ? "Image" : "Document",
            FileSize = file.Length,
            SavedPath = normalizedPath
        }, SystemMessages.MSG08));
    }

    /// <summary>
    /// Tải / Xem tệp đính kèm bảng tin lớp
    /// </summary>
    [HttpGet("attachment-file")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAttachmentFile(
        [FromQuery] string path,
        [FromServices] Infrastructure.ExternalServices.IFileStorageService fileStorageService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest();
        }

        var cleanPath = path.Replace("..", "").TrimStart('/', '\\');
        var stream = await fileStorageService.GetFileAsync(cleanPath, cancellationToken);
        if (stream == null)
        {
            return NotFound("Không tìm thấy tệp đính kèm.");
        }

        var ext = Path.GetExtension(cleanPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

        return File(stream, contentType, Path.GetFileName(cleanPath));
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
