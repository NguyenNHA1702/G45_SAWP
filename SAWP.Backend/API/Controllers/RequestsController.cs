using System.IO;
using System.Text;
using API.Extensions;
using Core.Constants;
using Core.DTOs;
using Core.Enums;
using Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    /// Tải lên tệp/ảnh minh chứng đính kèm (ảnh, PDF, Word)
    /// </summary>
    [HttpPost("upload-attachment")]
    public async Task<IActionResult> UploadAttachment(
        IFormFile file,
        [FromServices] IFileStorageService fileStorageService,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<AttachmentUploadResultDto>.Fail("Vui lòng chọn tệp tin cần tải lên."));
        }

        if (file.Length > 15 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<AttachmentUploadResultDto>.Fail("Kích thước tệp không được vượt quá 15MB."));
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse<AttachmentUploadResultDto>.Fail("Định dạng tệp không được hỗ trợ. Vui lòng chọn ảnh (.jpg, .png) hoặc tài liệu (.pdf, .doc, .docx)."));
        }

        using var stream = file.OpenReadStream();
        var relativePath = await fileStorageService.SaveFileAsync(stream, file.FileName, "request_attachments", cancellationToken);
        var normalizedPath = relativePath.Replace('\\', '/');
        var fileUrl = $"/api/LeaveRequests/attachment-file?path={Uri.EscapeDataString(normalizedPath)}";

        return Ok(ApiResponse<AttachmentUploadResultDto>.Ok(new AttachmentUploadResultDto
        {
            Url = fileUrl,
            FileName = file.FileName,
            FileSize = file.Length,
            SavedPath = normalizedPath
        }, "Tải lên tệp đính kèm thành công."));
    }

    /// <summary>
    /// Tải văn bản xác nhận điện tử chính thức theo thể thức Nghị định 30/2020/NĐ-CP (Định dạng Word .doc)
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
        var certNo = string.IsNullOrWhiteSpace(doc.CertificateNumber) ? "SAWP-DOC-OFFICIAL" : doc.CertificateNumber;
        var issuedDate = doc.IssuedDate ?? DateTime.UtcNow;

        var isTransferDoc = doc.DocumentType.Contains("chuyển trường", StringComparison.OrdinalIgnoreCase);
        var documentTitle = isTransferDoc 
            ? "ĐƠN XIN CHUYỂN TRƯỜNG & Ý KIẾN ĐỒNG Ý CỦA BAN GIÁM HIỆU" 
            : doc.DocumentType.ToUpper();
        var recipientLine = isTransferDoc
            ? "Kính gửi: Ban Giám Hiệu trường chuyển đến, Quý phụ huynh học sinh"
            : "Kính gửi: Các cơ quan, tổ chức, đơn vị có liên quan";
        var sectionTitle = isTransferDoc
            ? "HIỆU TRƯỞNG TRƯỜNG TIỂU HỌC SAWP XÁC NHẬN VÀ ĐỒNG Ý:"
            : "HIỆU TRƯỞNG TRƯỜNG TIỂU HỌC SAWP XÁC NHẬN:";
        var contentHeading = isTransferDoc
            ? "Ý kiến phê duyệt & nội dung xác nhận của Ban Giám Hiệu:"
            : "Nội dung xác nhận cụ thể:";
        var legalFooter = isTransferDoc
            ? "Văn bản này được cấp theo đúng thẩm quyền và Điều lệ trường Tiểu học (Thông tư 28/2020/TT-BGDĐT của Bộ Giáo dục và Đào tạo) để làm căn cứ cho gia đình học sinh liên hệ thủ tục tiếp nhận tại trường học mới."
            : "Giấy xác nhận này được cấp theo đúng thẩm quyền và quy chế công tác văn thư theo Nghị định 30/2020/NĐ-CP của Chính phủ để phụ huynh và học sinh hoàn thiện các thủ tục hành chính có liên quan.";

        var html = $@"<!DOCTYPE html>
<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
<head>
<meta charset='utf-8'>
<title>{documentTitle}</title>
<style>
  @page Section1 {{
    size: 210mm 297mm;
    margin: 20mm 20mm 20mm 25mm;
  }}
  div.Section1 {{ page: Section1; }}
  body {{
    font-family: 'Times New Roman', Times, serif;
    font-size: 13pt;
    line-height: 1.45;
    color: #000000;
  }}
  table.header-table {{
    width: 100%;
    border-collapse: collapse;
    margin-bottom: 24px;
  }}
  table.header-table td {{
    vertical-align: top;
    padding: 0 4px;
  }}
  .bold {{ font-weight: bold; }}
  .italic {{ font-style: italic; }}
  .uppercase {{ text-transform: uppercase; }}
  .title {{
    font-size: 15pt;
    font-weight: bold;
    text-align: center;
    margin-top: 20px;
    margin-bottom: 15px;
    text-transform: uppercase;
  }}
  .content-p {{
    text-align: justify;
    text-indent: 1.25cm;
    margin: 6px 0;
  }}
  .signature-table {{
    width: 100%;
    margin-top: 30px;
    border-collapse: collapse;
  }}
  .signature-table td {{
    vertical-align: top;
    padding: 0 8px;
  }}
  .badge-signed {{
    border: 2px solid #047857;
    background-color: #ECFDF5;
    color: #065F46;
    padding: 8px 12px;
    border-radius: 6px;
    font-size: 10.5pt;
    margin-top: 10px;
    display: inline-block;
  }}
</style>
</head>
<body>
<div class='Section1'>
  <table class='header-table'>
    <tr>
      <td style='width: 45%; text-align: center;'>
        <div style='font-size: 11pt;'>UBND THÀNH PHỐ HÀ NỘI</div>
        <div class='bold uppercase' style='font-size: 12pt;'>TRƯỜNG TIỂU HỌC SAWP</div>
        <div style='border-bottom: 1px solid #000; width: 120px; margin: 4px auto;'></div>
        <div style='font-size: 11pt; margin-top: 6px;'>Số: <strong>{certNo}</strong></div>
      </td>
      <td style='width: 55%; text-align: center;'>
        <div class='bold uppercase' style='font-size: 12pt;'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
        <div class='bold' style='font-size: 13pt;'>Độc lập - Tự do - Hạnh phúc</div>
        <div style='border-bottom: 1px solid #000; width: 160px; margin: 4px auto;'></div>
        <div class='italic' style='font-size: 12pt; margin-top: 6px;'>Hà Nội, ngày {issuedDate:dd} tháng {issuedDate:MM} năm {issuedDate:yyyy}</div>
      </td>
    </tr>
  </table>

  <div class='title'>{documentTitle}</div>

  <div style='text-align: center; margin-bottom: 20px;' class='italic'>
    {recipientLine}
  </div>

  <p class='content-p'>
    Căn cứ Thông tư số 28/2020/TT-BGDĐT ngày 04 tháng 9 năm 2020 của Bộ Giáo dục và Đào tạo ban hành Điều lệ trường Tiểu học;
  </p>
  <p class='content-p'>
    Căn cứ hồ sơ quản lý học sinh và đề nghị của phụ huynh tại đơn số <strong>{result.Data.RequestNumber}</strong>;
  </p>
  <p class='content-p'>
    <strong>{sectionTitle}</strong>
  </p>

  <table style='width: 100%; border-collapse: collapse; margin: 10px 0 15px;'>
    <tr>
      <td style='width: 35%; padding: 4px 0;'>Họ và tên học sinh:</td>
      <td class='bold uppercase' style='padding: 4px 0;'>{result.Data.StudentName}</td>
    </tr>
    <tr>
      <td style='padding: 4px 0;'>Mã định danh học sinh:</td>
      <td class='bold' style='padding: 4px 0;'>{result.Data.StudentCode}</td>
    </tr>
    <tr>
      <td style='padding: 4px 0;'>Hiện là học sinh lớp:</td>
      <td style='padding: 4px 0;'>{result.Data.CurrentClassName ?? "Học sinh chính khóa"} - Trường Tiểu học SAWP</td>
    </tr>
    <tr>
      <td style='padding: 4px 0;'>Năm học:</td>
      <td style='padding: 4px 0;'>2026 - 2027</td>
    </tr>
    <tr>
      <td style='padding: 4px 0;'>Lý do / Mục đích:</td>
      <td style='padding: 4px 0;'>{doc.Purpose}</td>
    </tr>
    <tr>
      <td style='padding: 4px 0;'>Số bản phát hành:</td>
      <td style='padding: 4px 0;'>{doc.NumberOfCopies} bản chính có giá trị pháp lý như nhau</td>
    </tr>
  </table>

  <p class='content-p'>
    <strong>{contentHeading}</strong><br />
    {doc.DocumentContentDraft?.Replace("\n", "<br />") ?? "Học sinh hiện đang theo học văn hóa chính khóa đầy đủ tại trường, chấp hành tốt nội quy và tham gia các hoạt động giáo dục toàn diện của nhà trường."}
  </p>

  <p class='content-p'>
    {legalFooter}
  </p>

  <table class='signature-table'>
    <tr>
      <td style='width: 45%;'>
        <div class='bold italic' style='font-size: 11pt;'>Nơi nhận:</div>
        <div style='font-size: 10.5pt;'>- Như trên;</div>
        <div style='font-size: 10.5pt;'>- Phụ huynh học sinh;</div>
        <div style='font-size: 10.5pt;'>- Lưu: VT, HS.</div>
      </td>
      <td style='width: 55%; text-align: center;'>
        <div class='bold uppercase' style='font-size: 12.5pt;'>TM. BAN GIÁM HIỆU</div>
        <div class='bold' style='font-size: 12.5pt;'>HIỆU TRƯỞNG</div>
        <div style='height: 40px;'></div>
        <div class='badge-signed'>
          ✓ <strong>CHỨNG THƯ KÝ SỐ ĐIỆN TỬ BAN GIÁM HIỆU</strong><br />
          {doc.DigitalSignatureInfo ?? "Ký số xác thực bởi Ban Giám Hiệu"}<br />
          Ngày ký duyệt: {issuedDate:dd/MM/yyyy HH:mm}
        </div>
      </td>
    </tr>
  </table>
</div>
</body>
</html>";

        var bytes = Encoding.UTF8.GetBytes(html);
        var fileNamePrefix = isTransferDoc ? "Don_Xin_Chuyen_Truong" : "Van_Ban_Xac_Nhan";
        var fileName = $"{fileNamePrefix}_{result.Data.StudentCode}_{result.Data.RequestNumber}.doc";
        return File(bytes, "application/msword; charset=utf-8", fileName);
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
