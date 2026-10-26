using System.Text;
using API.Extensions;
using Core.Constants;
using Core.DTOs;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RequestsController : ControllerBase
{
    private readonly IRequestWorkflowEngine _workflowEngine;

    public RequestsController(IRequestWorkflowEngine workflowEngine)
    {
        _workflowEngine = workflowEngine;
    }

    #region Parent Endpoints

    /// <summary>
    /// Phụ huynh nộp đơn xin chuyển lớp
    /// </summary>
    [HttpPost("class-transfer")]
    [Authorize(Roles = "Parent,Admin")]
    public async Task<IActionResult> SubmitClassTransfer(
        [FromBody] ClassTransferRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.SubmitClassTransferRequestAsync(userId.Value, request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Phụ huynh nộp đơn xin cấp giấy tờ / chứng nhận
    /// </summary>
    [HttpPost("document")]
    [Authorize(Roles = "Parent,Admin")]
    public async Task<IActionResult> SubmitDocument(
        [FromBody] DocumentIssuanceRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.SubmitDocumentRequestAsync(userId.Value, request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Phụ huynh xem danh sách các đơn đã gửi của con
    /// </summary>
    [HttpGet("my-requests")]
    [Authorize(Roles = "Parent,Admin")]
    public async Task<IActionResult> GetMyRequests(
        [FromQuery] int? studentId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.GetMyRequestsAsync(userId.Value, studentId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Phụ huynh nộp lại đơn sau khi bổ sung thông tin theo yêu cầu của Giáo vụ
    /// </summary>
    [HttpPut("{id:int}/resubmit")]
    [Authorize(Roles = "Parent,Admin")]
    public async Task<IActionResult> ResubmitRequest(
        int id,
        [FromBody] ResubmitRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.ResubmitRequestAsync(id, userId.Value, request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    #endregion

    #region Shared Endpoints

    /// <summary>
    /// Xem chi tiết một đơn hành chính
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRequestDetail(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.GetRequestDetailAsync(id, userId.Value, cancellationToken);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Tải văn bản xác nhận điện tử đã được BGH ký duyệt
    /// </summary>
    [HttpGet("{id:int}/download-document")]
    public async Task<IActionResult> DownloadIssuedDocument(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.GetRequestDetailAsync(id, userId.Value, cancellationToken);
        if (!result.Success || result.Data?.DocumentRequest == null)
        {
            return NotFound("Không tìm thấy văn bản phát hành.");
        }

        var doc = result.Data.DocumentRequest;
        var sb = new StringBuilder();
        sb.AppendLine("==========================================================================");
        sb.AppendLine("                 CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM                      ");
        sb.AppendLine("                       Độc lập - Tự do - Hạnh phúc                        ");
        sb.AppendLine("==========================================================================");
        sb.AppendLine();
        sb.AppendLine("               TRƯỜNG TIỂU HỌC SAWP - TP. HÀ NỘI                          ");
        sb.AppendLine($"Số hiệu văn bản: {doc.CertificateNumber ?? "SAWP-DOC-OFFICIAL"}");
        sb.AppendLine($"Mã đơn tra cứu: {result.Data.RequestNumber}");
        sb.AppendLine();
        sb.AppendLine($"               {doc.DocumentType.ToUpper()}                               ");
        sb.AppendLine();
        sb.AppendLine($"Kính gửi: Các cơ quan, tổ chức có liên quan.");
        sb.AppendLine();
        sb.AppendLine($"Ban Giám Hiệu Trường Tiểu học SAWP trân trọng xác nhận:");
        sb.AppendLine($"- Học sinh: {result.Data.StudentName}");
        sb.AppendLine($"- Mã học sinh: {result.Data.StudentCode}");
        sb.AppendLine($"- Hiện đang theo học chính khóa tại Trường Tiểu học SAWP.");
        sb.AppendLine($"- Mục đích cấp giấy: {doc.Purpose}");
        sb.AppendLine($"- Số lượng bản phát hành: {doc.NumberOfCopies} bản.");
        sb.AppendLine();
        sb.AppendLine("NỘI DUNG XÁC NHẬN CHI TIẾT:");
        sb.AppendLine(doc.DocumentContentDraft ?? "Học sinh có hạnh kiểm Tốt, chấp hành nghiêm túc nội quy nhà trường.");
        sb.AppendLine();
        sb.AppendLine("XÁC NHẬN CHỮ KÝ SỐ CHÍNH THỨC:");
        sb.AppendLine($"- Cơ quan phát hành: Ban Giám Hiệu Trường Tiểu học SAWP");
        sb.AppendLine($"- Trạng thái: {doc.DigitalSignatureInfo ?? "Đã ký số điện tử hợp lệ"}");
        sb.AppendLine($"- Ngày ký phát hành: {doc.IssuedDate:dd/MM/yyyy HH:mm:ss} (UTC)");
        sb.AppendLine();
        sb.AppendLine("==========================================================================");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"Giay_Xac_Nhan_{result.Data.StudentCode}_{result.Data.RequestNumber}.txt";
        return File(bytes, "text/plain; charset=utf-8", fileName);
    }

    #endregion

    #region Office Staff Endpoints

    /// <summary>
    /// Giáo vụ lấy danh sách đơn hành chính cần thẩm định
    /// </summary>
    [HttpGet("office")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    public async Task<IActionResult> GetOfficeRequests(
        [FromQuery] string? requestType,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _workflowEngine.GetOfficeStaffRequestsAsync(requestType, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Giáo vụ yêu cầu phụ huynh bổ sung/sửa đổi hồ sơ
    /// </summary>
    [HttpPost("{id:int}/request-amendment")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    public async Task<IActionResult> RequestAmendment(
        int id,
        [FromBody] StaffAmendmentDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.RequestAmendmentAsync(id, userId.Value, request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Giáo vụ thẩm định sức chứa và gửi đề xuất chuyển lớp lên BGH
    /// </summary>
    [HttpPost("{id:int}/endorse-transfer")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    public async Task<IActionResult> EndorseTransfer(
        int id,
        [FromBody] StaffEndorseTransferDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.EndorseTransferAsync(id, userId.Value, request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Giáo vụ chuẩn bị dự thảo văn bản và gửi lên BGH phê duyệt
    /// </summary>
    [HttpPost("{id:int}/endorse-document")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    public async Task<IActionResult> EndorseDocument(
        int id,
        [FromBody] StaffEndorseDocumentDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.EndorseDocumentAsync(id, userId.Value, request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Giáo vụ thực thi bấm "Áp dụng chuyển lớp" sau khi BGH đã phê duyệt
    /// </summary>
    [HttpPost("{id:int}/execute-transfer")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    public async Task<IActionResult> ExecuteClassTransfer(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.ExecuteClassTransferAsync(id, userId.Value, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    #endregion

    #region School Board Endpoints

    /// <summary>
    /// Ban Giám Hiệu lấy danh sách các đề xuất chờ phê duyệt
    /// </summary>
    [HttpGet("board")]
    [Authorize(Roles = "SchoolBoard,Admin")]
    public async Task<IActionResult> GetBoardRequests(
        [FromQuery] string? requestType,
        CancellationToken cancellationToken)
    {
        var result = await _workflowEngine.GetSchoolBoardRequestsAsync(requestType, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Ban Giám Hiệu ra quyết định phê duyệt hoặc từ chối đề xuất
    /// </summary>
    [HttpPost("{id:int}/board-decision")]
    [Authorize(Roles = "SchoolBoard,Admin")]
    public async Task<IActionResult> ProcessBoardDecision(
        int id,
        [FromBody] BoardDecisionDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _workflowEngine.ProcessBoardDecisionAsync(id, userId.Value, request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    #endregion
}
