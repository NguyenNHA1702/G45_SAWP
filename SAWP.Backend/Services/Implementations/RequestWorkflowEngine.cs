using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class RequestWorkflowEngine : IRequestWorkflowEngine
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public RequestWorkflowEngine(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    #region Parent Operations

    public async Task<ApiResponse<string>> SubmitClassTransferRequestAsync(
        int requesterUserId,
        ClassTransferRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.StudentId <= 0 || request.TargetClassId <= 0 || string.IsNullOrWhiteSpace(request.TransferReason))
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn học sinh, lớp muốn chuyển và nhập lý do." });
        }

        // Validate Guardian relationship
        var guardian = await _unitOfWork.Repository<Guardian>()
            .Query()
            .Include(g => g.StudentGuardians)
            .FirstOrDefaultAsync(g => g.UserId == requesterUserId, cancellationToken);

        if (guardian == null || !guardian.StudentGuardians.Any(sg => sg.StudentId == request.StudentId))
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG10, new List<string> { "Bạn không có quyền gửi đơn cho học sinh này." });
        }

        var student = await _unitOfWork.Students.GetByIdAsync(request.StudentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG11);
        }

        // Get current active class assignment
        var currentAssignment = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .Include(ca => ca.Class)
            .FirstOrDefaultAsync(ca => ca.StudentId == request.StudentId && ca.IsActive, cancellationToken);

        if (currentAssignment == null)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Học sinh hiện chưa được xếp vào lớp nào nên không thể xin chuyển lớp." });
        }

        if (currentAssignment.ClassId == request.TargetClassId)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Lớp muốn chuyển tới trùng với lớp hiện tại của học sinh." });
        }

        var targetClass = await _unitOfWork.Repository<Class>().GetByIdAsync(request.TargetClassId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Lớp muốn chuyển tới không tồn tại." });
        }

        var reqNumber = $"REQ-CT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var adminRequest = new AdministrativeRequest
        {
            RequestNumber = reqNumber,
            RequesterUserId = requesterUserId,
            StudentId = request.StudentId,
            RequestType = RequestTypes.ClassTransfer,
            Status = RequestStatus.Submitted,
            AttachmentUrl = request.AttachmentUrl,
            SubmissionDate = DateTime.UtcNow
        };

        await _unitOfWork.Repository<AdministrativeRequest>().AddAsync(adminRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var transferDetail = new ClassTransferDetail
        {
            AdministrativeRequestId = adminRequest.Id,
            FromClassId = currentAssignment.ClassId,
            ToClassId = request.TargetClassId,
            TransferReason = request.TransferReason.Trim()
        };

        await _unitOfWork.Repository<ClassTransferDetail>().AddAsync(transferDetail, cancellationToken);

        // Notify Office Staff
        await NotifyStaffMembersAsync(
            "Yêu cầu chuyển lớp mới",
            $"Phụ huynh học sinh {student.FullName} vừa nộp đơn xin chuyển từ lớp {currentAssignment.Class.ClassName} sang {targetClass.ClassName}.",
            "/office/requests",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<string>.Ok(reqNumber, SystemMessages.MSG30);
    }

    public async Task<ApiResponse<string>> SubmitDocumentRequestAsync(
        int requesterUserId,
        DocumentIssuanceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.StudentId <= 0 || string.IsNullOrWhiteSpace(request.DocumentType) || string.IsNullOrWhiteSpace(request.Purpose))
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn học sinh, loại giấy tờ và mục đích xin cấp." });
        }

        var guardian = await _unitOfWork.Repository<Guardian>()
            .Query()
            .Include(g => g.StudentGuardians)
            .FirstOrDefaultAsync(g => g.UserId == requesterUserId, cancellationToken);

        if (guardian == null || !guardian.StudentGuardians.Any(sg => sg.StudentId == request.StudentId))
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG10, new List<string> { "Bạn không có quyền gửi đơn cho học sinh này." });
        }

        var student = await _unitOfWork.Students.GetByIdAsync(request.StudentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG11);
        }

        var reqNumber = $"REQ-DOC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var adminRequest = new AdministrativeRequest
        {
            RequestNumber = reqNumber,
            RequesterUserId = requesterUserId,
            StudentId = request.StudentId,
            RequestType = RequestTypes.Document,
            Status = RequestStatus.Submitted,
            AttachmentUrl = request.AttachmentUrl,
            SubmissionDate = DateTime.UtcNow
        };

        await _unitOfWork.Repository<AdministrativeRequest>().AddAsync(adminRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var docDetail = new DocumentRequestDetail
        {
            AdministrativeRequestId = adminRequest.Id,
            DocumentType = request.DocumentType.Trim(),
            NumberOfCopies = request.NumberOfCopies > 0 ? request.NumberOfCopies : 1,
            Purpose = request.Purpose.Trim()
        };

        await _unitOfWork.Repository<DocumentRequestDetail>().AddAsync(docDetail, cancellationToken);

        // Notify Office Staff
        await NotifyStaffMembersAsync(
            "Yêu cầu cấp giấy tờ / chứng nhận mới",
            $"Phụ huynh học sinh {student.FullName} vừa nộp đơn xin cấp {docDetail.DocumentType} ({docDetail.NumberOfCopies} bản).",
            "/office/requests",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<string>.Ok(reqNumber, SystemMessages.MSG30);
    }

    public async Task<ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>> GetMyRequestsAsync(
        int parentUserId,
        int? studentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.RequesterUser)
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.FromClass)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass)
            .Include(r => r.DocumentRequestDetail)
            .Where(r => r.RequesterUserId == parentUserId && (r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document));

        if (studentId.HasValue)
        {
            query = query.Where(r => r.StudentId == studentId.Value);
        }

        var list = await query.OrderByDescending(r => r.SubmissionDate).ToListAsync(cancellationToken);

        var dtos = list.Select(MapToSummaryDto).ToList();
        return ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AdministrativeRequestSummaryDto>> GetRequestDetailAsync(
        int requestId,
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.RequesterUser)
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.FromClass)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass)
            .Include(r => r.DocumentRequestDetail)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<AdministrativeRequestSummaryDto>.Fail("Không tìm thấy yêu cầu.");
        }

        return ApiResponse<AdministrativeRequestSummaryDto>.Ok(MapToSummaryDto(req));
    }

    public async Task<ApiResponse<bool>> ResubmitRequestAsync(
        int requestId,
        int requesterUserId,
        ResubmitRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail)
            .Include(r => r.DocumentRequestDetail)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu.");
        }

        if (req.RequesterUserId != requesterUserId)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG10);
        }

        if (!req.RequiresAmendment)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn này hiện không ở trạng thái yêu cầu bổ sung." });
        }

        if (req.ClassTransferDetail != null && !string.IsNullOrWhiteSpace(request.TransferReason))
        {
            req.ClassTransferDetail.TransferReason = request.TransferReason.Trim();
        }

        if (req.DocumentRequestDetail != null && !string.IsNullOrWhiteSpace(request.Purpose))
        {
            req.DocumentRequestDetail.Purpose = request.Purpose.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.AttachmentUrl))
        {
            req.AttachmentUrl = request.AttachmentUrl.Trim();
        }

        req.RequiresAmendment = false;
        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        // Notify Office Staff
        await NotifyStaffMembersAsync(
            "Phụ huynh đã cập nhật & nộp lại hồ sơ",
            $"Đơn {req.RequestNumber} của học sinh {req.Student?.FullName} đã được phụ huynh bổ sung hồ sơ. Vui lòng kiểm tra lại.",
            "/office/requests",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    #endregion

    #region Office Staff Operations

    public async Task<ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>> GetOfficeStaffRequestsAsync(
        string? requestType = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.RequesterUser)
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.FromClass)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass)
            .Include(r => r.DocumentRequestDetail)
            .Where(r => r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document);

        if (!string.IsNullOrWhiteSpace(requestType))
        {
            query = query.Where(r => r.RequestType == requestType);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, true, out var st))
        {
            query = query.Where(r => r.Status == st);
        }

        var list = await query.OrderByDescending(r => r.SubmissionDate).ToListAsync(cancellationToken);
        var dtos = list.Select(MapToSummaryDto).ToList();
        return ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<bool>> RequestAmendmentAsync(
        int requestId,
        int staffUserId,
        StaffAmendmentDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AmendmentNotes))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập nội dung yêu cầu bổ sung." });
        }

        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu.");
        }

        req.RequiresAmendment = true;
        req.AmendmentNotes = request.AmendmentNotes.Trim();
        req.ReviewerUserId = staffUserId;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        // Send Notification to Parent
        var notif = new Notification
        {
            UserId = req.RequesterUserId,
            Title = $"Yêu cầu bổ sung thông tin đơn {req.RequestNumber}",
            Content = $"Giáo vụ đã gửi yêu cầu bổ sung hồ sơ: {request.AmendmentNotes.Trim()}",
            Type = "AMENDMENT_REQUIRED",
            LinkUrl = "/parent/requests"
        };
        await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> EndorseTransferAsync(
        int requestId,
        int staffUserId,
        StaffEndorseTransferDto request,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null || req.ClassTransferDetail == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn xin chuyển lớp.");
        }

        // Capacity check
        var targetClassId = req.ClassTransferDetail.ToClassId;
        var currentCount = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .CountAsync(ca => ca.ClassId == targetClassId && ca.IsActive, cancellationToken);

        if (req.ClassTransferDetail.ToClass != null && currentCount >= req.ClassTransferDetail.ToClass.Capacity)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG15, new List<string> { $"Lớp {req.ClassTransferDetail.ToClass.ClassName} đã đủ sĩ số ({currentCount}/{req.ClassTransferDetail.ToClass.Capacity})." });
        }

        req.ClassTransferDetail.StaffRecommendation = request.StaffRecommendation.Trim();
        req.ClassTransferDetail.TargetClassCapacitySnapshot = currentCount;
        req.Status = RequestStatus.UnderReview;
        req.ReviewerUserId = staffUserId;
        req.RequiresAmendment = false;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        // Notify SchoolBoard
        await NotifyBoardMembersAsync(
            "Đề xuất chuyển lớp chờ phê duyệt",
            $"Giáo vụ đã thẩm định và đề xuất chuyển lớp cho học sinh {req.Student?.FullName} (Đơn {req.RequestNumber}).",
            "/board/requests",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> EndorseDocumentAsync(
        int requestId,
        int staffUserId,
        StaffEndorseDocumentDto request,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.Student)
            .Include(r => r.DocumentRequestDetail)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null || req.DocumentRequestDetail == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn xin cấp giấy tờ.");
        }

        req.DocumentRequestDetail.StaffEndorsement = request.StaffEndorsement.Trim();
        req.DocumentRequestDetail.DocumentContentDraft = string.IsNullOrWhiteSpace(request.DocumentContentDraft)
            ? $"DỰ THẢO VĂN BẢN XÁC NHẬN:\nTrường Tiểu học SAWP xác nhận học sinh {req.Student?.FullName} (Mã: {req.Student?.StudentCode}) đang theo học hợp lệ tại trường.\nMục đích cấp: {req.DocumentRequestDetail.Purpose}."
            : request.DocumentContentDraft.Trim();

        req.Status = RequestStatus.UnderReview;
        req.ReviewerUserId = staffUserId;
        req.RequiresAmendment = false;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        // Notify SchoolBoard
        await NotifyBoardMembersAsync(
            "Hồ sơ xin cấp giấy tờ chờ phê duyệt",
            $"Giáo vụ đã chuẩn bị dự thảo {req.DocumentRequestDetail.DocumentType} cho học sinh {req.Student?.FullName} (Đơn {req.RequestNumber}).",
            "/board/requests",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> ExecuteClassTransferAsync(
        int requestId,
        int staffUserId,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.FromClass)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null || req.ClassTransferDetail == null || req.StudentId == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn chuyển lớp hợp lệ.");
        }

        if (req.SchoolBoardApprovedAt == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn chuyển lớp chưa được Ban Giám Hiệu phê duyệt." });
        }

        if (req.ClassTransferDetail.IsExecuted)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn chuyển lớp này đã được thực thi trước đó." });
        }

        var studentId = req.StudentId.Value;
        var toClassId = req.ClassTransferDetail.ToClassId;

        // 1. Deactivate old active class assignment (BR-18: preserve history)
        var oldAssignment = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .FirstOrDefaultAsync(ca => ca.StudentId == studentId && ca.IsActive, cancellationToken);

        if (oldAssignment != null)
        {
            oldAssignment.IsActive = false;
            oldAssignment.EndDate = DateTime.UtcNow;
            await _unitOfWork.Repository<ClassAssignment>().UpdateAsync(oldAssignment, cancellationToken);
        }

        // 2. Add new active class assignment
        var newAssignment = new ClassAssignment
        {
            ClassId = toClassId,
            StudentId = studentId,
            StartDate = DateTime.UtcNow,
            IsActive = true
        };
        await _unitOfWork.Repository<ClassAssignment>().AddAsync(newAssignment, cancellationToken);

        // 3. Mark ClassTransferDetail as executed
        req.ClassTransferDetail.IsExecuted = true;
        req.ClassTransferDetail.ExecutedAt = DateTime.UtcNow;
        req.ProcessedDate = DateTime.UtcNow;
        req.Status = RequestStatus.Approved;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        // 4. Send notification with new class details to Parent
        var notif = new Notification
        {
            UserId = req.RequesterUserId,
            Title = "Đã cập nhật chuyển lớp thành công",
            Content = $"Học sinh {req.Student?.FullName} đã được chuyển sang lớp {req.ClassTransferDetail.ToClass?.ClassName} thành công. Vui lòng kiểm tra thời khóa biểu mới của con.",
            Type = "CLASS_TRANSFER_COMPLETED",
            LinkUrl = "/parent/children"
        };
        await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);

        // 5. Audit Log
        await _auditService.LogActionAsync(
            staffUserId,
            "ExecuteClassTransfer",
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            $"FromClass: {req.ClassTransferDetail.FromClass?.ClassName}",
            $"ToClass: {req.ClassTransferDetail.ToClass?.ClassName}",
            null,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG35);
    }

    #endregion

    #region School Board Operations

    public async Task<ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>> GetSchoolBoardRequestsAsync(
        string? requestType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.RequesterUser)
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.FromClass)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass)
            .Include(r => r.DocumentRequestDetail)
            .Where(r => r.Status == RequestStatus.UnderReview && (r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document));

        if (!string.IsNullOrWhiteSpace(requestType))
        {
            query = query.Where(r => r.RequestType == requestType);
        }

        var list = await query.OrderBy(r => r.SubmissionDate).ToListAsync(cancellationToken);
        var dtos = list.Select(MapToSummaryDto).ToList();
        return ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<bool>> ProcessBoardDecisionAsync(
        int requestId,
        int boardUserId,
        BoardDecisionDto request,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.FromClass)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass)
            .Include(r => r.DocumentRequestDetail)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu.");
        }

        if (req.Status != RequestStatus.UnderReview)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Yêu cầu chưa được Giáo vụ thẩm định hoặc đã được xử lý trước đó." });
        }

        if (request.IsApproved)
        {
            req.SchoolBoardApprovedAt = DateTime.UtcNow;
            req.SchoolBoardNotes = request.DecisionNotes?.Trim();
            req.ReviewerUserId = boardUserId;

            if (req.RequestType == RequestTypes.ClassTransfer)
            {
                // BGH approves -> Route back to Office Staff to execute transfer
                req.Status = RequestStatus.Approved;
                req.ReviewerNotes = "BGH đã duyệt đơn chuyển lớp. Đang chờ Giáo vụ thực thi điều chuyển.";

                await NotifyStaffMembersAsync(
                    "BGH đã duyệt đơn chuyển lớp",
                    $"Đơn chuyển lớp {req.RequestNumber} của học sinh {req.Student?.FullName} đã được BGH duyệt. Vui lòng thực hiện thao tác chuyển lớp.",
                    "/office/requests",
                    cancellationToken);
            }
            else if (req.RequestType == RequestTypes.Document && req.DocumentRequestDetail != null)
            {
                // BGH approves -> Digitally sign and issue document
                var certNum = $"SAWP-{DateTime.UtcNow:yyyy}-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
                req.DocumentRequestDetail.CertificateNumber = certNum;
                req.DocumentRequestDetail.DigitalSignatureInfo = $"Ký số xác thực bởi Ban Giám Hiệu ngày {DateTime.UtcNow:dd/MM/yyyy HH:mm}";
                req.DocumentRequestDetail.IssuedDate = DateTime.UtcNow;
                req.DocumentRequestDetail.IssuedDocumentUrl = $"/api/requests/{req.Id}/download-document";

                req.Status = RequestStatus.Approved;
                req.ProcessedDate = DateTime.UtcNow;

                // Send notification to Parent with download link
                var notif = new Notification
                {
                    UserId = req.RequesterUserId,
                    Title = "Tài liệu / Giấy xác nhận đã được phát hành",
                    Content = $"Văn bản {req.DocumentRequestDetail.DocumentType} cho học sinh {req.Student?.FullName} đã được Ban Giám Hiệu ký duyệt và phát hành chính thức.",
                    Type = "DOCUMENT_ISSUED",
                    LinkUrl = req.DocumentRequestDetail.IssuedDocumentUrl
                };
                await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
            }

            await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Ok(true, SystemMessages.MSG31);
        }
        else
        {
            // Rejection requires decision notes
            if (string.IsNullOrWhiteSpace(request.DecisionNotes))
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập lý do từ chối yêu cầu." });
            }

            req.Status = RequestStatus.Rejected;
            req.ProcessedDate = DateTime.UtcNow;
            req.SchoolBoardNotes = request.DecisionNotes.Trim();
            req.ReviewerUserId = boardUserId;

            await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

            // Audit Log rejection reason
            await _auditService.LogActionAsync(
                boardUserId,
                "RejectAdministrativeRequest",
                nameof(AdministrativeRequest),
                req.Id.ToString(),
                null,
                $"Type: {req.RequestType}, Rejection Reason: {request.DecisionNotes.Trim()}",
                null,
                cancellationToken);

            // Notify Parent
            var notif = new Notification
            {
                UserId = req.RequesterUserId,
                Title = "Yêu cầu hành chính không được phê duyệt",
                Content = $"Yêu cầu {req.RequestNumber} ({req.RequestType}) không được BGH phê duyệt. Lý do: {request.DecisionNotes.Trim()}",
                Type = "REQUEST_REJECTED",
                LinkUrl = "/parent/requests"
            };
            await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Ok(true, SystemMessages.MSG32);
        }
    }

    #endregion

    #region Legacy & Helper Methods

    public async Task<ApiResponse<bool>> ProcessRequestAsync(
        int requestId,
        int reviewerUserId,
        ProcessRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>().GetByIdAsync(requestId, cancellationToken);
        if (req == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu.");
        }

        req.Status = request.NewStatus;
        req.ReviewerNotes = request.ReviewerNotes;
        req.ReviewerUserId = reviewerUserId;
        req.ProcessedDate = DateTime.UtcNow;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    private static AdministrativeRequestSummaryDto MapToSummaryDto(AdministrativeRequest r)
    {
        var dto = new AdministrativeRequestSummaryDto
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber,
            RequestType = r.RequestType,
            Status = r.Status,
            SubmissionDate = r.SubmissionDate,
            ProcessedDate = r.ProcessedDate,
            RequesterUserId = r.RequesterUserId,
            RequesterName = r.RequesterUser?.FullName ?? "Phụ huynh",
            StudentId = r.StudentId,
            StudentCode = r.Student?.StudentCode,
            StudentName = r.Student?.FullName,
            RequiresAmendment = r.RequiresAmendment,
            AttachmentUrl = r.AttachmentUrl,
            AmendmentNotes = r.AmendmentNotes,
            ReviewerNotes = r.ReviewerNotes,
            SchoolBoardNotes = r.SchoolBoardNotes,
            SchoolBoardApprovedAt = r.SchoolBoardApprovedAt
        };

        if (r.ClassTransferDetail != null)
        {
            dto.ClassTransfer = new ClassTransferSummaryDto
            {
                FromClassId = r.ClassTransferDetail.FromClassId,
                FromClassName = r.ClassTransferDetail.FromClass?.ClassName ?? string.Empty,
                ToClassId = r.ClassTransferDetail.ToClassId,
                ToClassName = r.ClassTransferDetail.ToClass?.ClassName ?? string.Empty,
                TransferReason = r.ClassTransferDetail.TransferReason,
                StaffRecommendation = r.ClassTransferDetail.StaffRecommendation,
                TargetClassCapacity = r.ClassTransferDetail.TargetClassCapacitySnapshot,
                TargetClassMaxCapacity = r.ClassTransferDetail.ToClass?.Capacity,
                IsExecuted = r.ClassTransferDetail.IsExecuted,
                ExecutedAt = r.ClassTransferDetail.ExecutedAt
            };
            dto.CurrentClassName = r.ClassTransferDetail.FromClass?.ClassName;
        }

        if (r.DocumentRequestDetail != null)
        {
            dto.DocumentRequest = new DocumentRequestSummaryDto
            {
                DocumentType = r.DocumentRequestDetail.DocumentType,
                NumberOfCopies = r.DocumentRequestDetail.NumberOfCopies,
                Purpose = r.DocumentRequestDetail.Purpose,
                StaffEndorsement = r.DocumentRequestDetail.StaffEndorsement,
                DocumentContentDraft = r.DocumentRequestDetail.DocumentContentDraft,
                IssuedDate = r.DocumentRequestDetail.IssuedDate,
                IssuedDocumentUrl = r.DocumentRequestDetail.IssuedDocumentUrl,
                CertificateNumber = r.DocumentRequestDetail.CertificateNumber,
                DigitalSignatureInfo = r.DocumentRequestDetail.DigitalSignatureInfo
            };
        }

        return dto;
    }

    private async Task NotifyStaffMembersAsync(string title, string content, string linkUrl, CancellationToken ct)
    {
        var staffUsers = await _unitOfWork.Repository<UserRole>()
            .Query()
            .Include(ur => ur.Role)
            .Where(ur => ur.Role.Name == UserRoles.OfficeStaff.ToString() || ur.Role.Name == UserRoles.Admin.ToString())
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var uId in staffUsers)
        {
            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                UserId = uId,
                Title = title,
                Content = content,
                Type = "ADMIN_REQUEST",
                LinkUrl = linkUrl
            }, ct);
        }
    }

    private async Task NotifyBoardMembersAsync(string title, string content, string linkUrl, CancellationToken ct)
    {
        var boardUsers = await _unitOfWork.Repository<UserRole>()
            .Query()
            .Include(ur => ur.Role)
            .Where(ur => ur.Role.Name == UserRoles.SchoolBoard.ToString() || ur.Role.Name == UserRoles.Admin.ToString())
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var uId in boardUsers)
        {
            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                UserId = uId,
                Title = title,
                Content = content,
                Type = "ADMIN_REQUEST",
                LinkUrl = linkUrl
            }, ct);
        }
    }

    #endregion
}
