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

        // Check if student already has a pending or in-progress transfer request (UC-36 E1)
        var hasPendingTransfer = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .AnyAsync(r => r.StudentId == request.StudentId 
                        && r.RequestType == RequestTypes.ClassTransfer 
                        && (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.UnderReview || (r.Status == RequestStatus.Approved && r.ClassTransferDetail != null && !r.ClassTransferDetail.IsExecuted)), cancellationToken);

        if (hasPendingTransfer)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG09, new List<string> { 
                "Học sinh hiện đang có một đơn xin chuyển lớp đang trong quá trình xử lý (UC-36 E1)." 
            });
        }

        // Validate target class capacity at submit time (BR-37)
        var targetEnrolled = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .CountAsync(ca => ca.ClassId == request.TargetClassId && ca.IsActive, cancellationToken);
        if (targetClass.Capacity > 0 && targetEnrolled >= targetClass.Capacity)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG15, new List<string> { 
                $"Lớp {targetClass.ClassName} đã đủ sĩ số ({targetEnrolled}/{targetClass.Capacity}). Không thể nộp đơn xin chuyển tới lớp này." 
            });
        }

        // Validate same grade transfer rule
        if (currentAssignment.Class != null && currentAssignment.Class.Grade != targetClass.Grade)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { 
                $"Chỉ được xin chuyển lớp trong cùng khối (Khối {currentAssignment.Class.Grade}). Lớp đích '{targetClass.ClassName}' thuộc Khối {targetClass.Grade}." 
            });
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
            TransferReason = request.TransferReason.Trim(),
            EffectiveDate = request.EffectiveDate ?? DateTime.UtcNow,
            TargetClassCapacitySnapshot = targetEnrolled
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

        if (request.NumberOfCopies < 0 || request.NumberOfCopies > 10)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Số lượng bản sao phải từ 1 đến 10 bản (không chấp nhận số âm hoặc vượt quá 10 bản)." });
        }

        var copies = request.NumberOfCopies == 0 ? 1 : request.NumberOfCopies;

        // Sanitize DocumentType
        var cleanDocType = System.Text.RegularExpressions.Regex.Replace(request.DocumentType, "<.*?>", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanDocType))
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Loại giấy tờ không hợp lệ." });
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

        // Anti-spam / duplicate check for Document requests
        var hasPendingDocRequest = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.DocumentRequestDetail)
            .AnyAsync(r => r.StudentId == request.StudentId
                        && r.RequestType == RequestTypes.Document
                        && (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.UnderReview)
                        && r.DocumentRequestDetail != null
                        && r.DocumentRequestDetail.DocumentType.ToLower() == cleanDocType.ToLower(),
                        cancellationToken);

        if (hasPendingDocRequest)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG09, new List<string> {
                $"Học sinh hiện đang có đơn xin cấp '{cleanDocType}' đang trong quá trình xử lý. Quý phụ huynh vui lòng chờ kết quả đơn trước đó."
            });
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
            DocumentType = cleanDocType,
            NumberOfCopies = copies,
            Purpose = request.Purpose.Trim(),
            IsPaperCopyRequested = request.IsPaperCopyRequested
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
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass).ThenInclude(c => c!.ClassAssignments)
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
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass).ThenInclude(c => c!.ClassAssignments)
            .Include(r => r.DocumentRequestDetail)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<AdministrativeRequestSummaryDto>.Fail("Không tìm thấy yêu cầu.");
        }

        // Authorization check: Admin, OfficeStaff, SchoolBoard or Student's Guardian / Requester or Homeroom Teacher (BUG8)
        var userRoles = await _unitOfWork.Repository<UserRole>()
            .Query()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == currentUserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken);

        bool isStaffOrAdmin = userRoles.Any(r => r == UserRoles.OfficeStaff.ToString() || r == UserRoles.SchoolBoard.ToString() || r == UserRoles.Admin.ToString());

        if (!isStaffOrAdmin)
        {
            bool isRequester = req.RequesterUserId == currentUserId || (req.RequesterUserId == 0 && userRoles.Count == 0);
            bool isGuardian = req.StudentId.HasValue && await _unitOfWork.Repository<StudentGuardian>()
                .Query()
                .Include(sg => sg.Guardian)
                .AnyAsync(sg => sg.StudentId == req.StudentId.Value && sg.Guardian.UserId == currentUserId, cancellationToken);

            bool isHomeroomTeacher = false;
            if (userRoles.Contains("Teacher") && req.StudentId.HasValue)
            {
                var teacherStaff = await _unitOfWork.Repository<StaffProfile>()
                    .Query()
                    .FirstOrDefaultAsync(sp => sp.UserId == currentUserId, cancellationToken);
                if (teacherStaff != null)
                {
                    isHomeroomTeacher = await _unitOfWork.Repository<ClassAssignment>()
                        .Query()
                        .Include(ca => ca.Class)
                        .AnyAsync(ca => ca.StudentId == req.StudentId.Value && ca.IsActive && ca.Class.HomeroomTeacherId == teacherStaff.Id, cancellationToken);
                }
            }

            if (!isRequester && !isGuardian && !isHomeroomTeacher)
            {
                return ApiResponse<AdministrativeRequestSummaryDto>.Fail(SystemMessages.MSG10);
            }
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

        if (req.DocumentRequestDetail != null)
        {
            if (!string.IsNullOrWhiteSpace(request.Purpose))
            {
                req.DocumentRequestDetail.Purpose = request.Purpose.Trim();
            }
            if (request.NumberOfCopies.HasValue && request.NumberOfCopies.Value >= 1 && request.NumberOfCopies.Value <= 10)
            {
                req.DocumentRequestDetail.NumberOfCopies = request.NumberOfCopies.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.AttachmentUrl))
        {
            req.AttachmentUrl = request.AttachmentUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.AdditionalNotes))
        {
            req.ReviewerNotes = $"Ghi chú bổ sung của phụ huynh: {request.AdditionalNotes.Trim()}";
        }

        req.RequiresAmendment = false;
        req.AmendmentNotes = null; // Clear old amendment notes per BUG 9
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

    public async Task<ApiResponse<bool>> CancelRequestAsync(
        int requestId,
        int requesterUserId,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu.");
        }

        var userRoles = await _unitOfWork.Repository<UserRole>()
            .Query()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == requesterUserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken);

        bool isAdmin = userRoles.Contains(UserRoles.Admin.ToString());
        if (req.RequesterUserId != requesterUserId && !isAdmin)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG10);
        }

        // Disallow cancellation once forwarded to School Board (UnderReview) or approved/rejected/completed (BUG6)
        if (req.Status != RequestStatus.Submitted)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG34, new List<string> { 
                "Yêu cầu không thể hủy ở trạng thái hiện tại (Đã trình Ban Giám Hiệu hoặc đã hoàn tất xử lý)." 
            });
        }

        req.Status = RequestStatus.Cancelled;
        req.ProcessedDate = DateTime.UtcNow;
        req.ReviewerNotes = "Phụ huynh tự hủy đơn.";

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        await _auditService.LogActionAsync(
            requesterUserId,
            "CancelRequest",
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            new { OldStatus = RequestStatus.Submitted },
            new { NewStatus = RequestStatus.Cancelled },
            null,
            cancellationToken);

        // Notify Staff
        await NotifyStaffMembersAsync(
            "Phụ huynh đã hủy đơn",
            $"Phụ huynh đã hủy đơn {req.RequestNumber} của học sinh {req.Student?.FullName}.",
            "/office/requests",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, "Đã hủy đơn thành công.");
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
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass).ThenInclude(c => c!.ClassAssignments)
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
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu.");
        }

        if (req.Status != RequestStatus.Submitted)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ có thể yêu cầu bổ sung hồ sơ khi đơn đang ở trạng thái 'Chờ thẩm định'." });
        }

        if (req.RequiresAmendment)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn này đã được gửi yêu cầu bổ sung trước đó. Vui lòng đợi phụ huynh hoàn tất và nộp lại hồ sơ." });
        }

        req.RequiresAmendment = true;
        req.AmendmentNotes = request.AmendmentNotes.Trim();
        req.ReviewerUserId = staffUserId;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        // Send Notification to Parent / Guardians
        await NotifyGuardiansOfStudentAsync(
            req.StudentId,
            req.RequesterUserId,
            $"Yêu cầu bổ sung thông tin đơn {req.RequestNumber}",
            $"Giáo vụ đã gửi yêu cầu bổ sung hồ sơ cho học sinh {req.Student?.FullName}: {request.AmendmentNotes.Trim()}",
            "AMENDMENT_REQUIRED",
            "/parent/requests",
            cancellationToken);

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

        if (req.RequiresAmendment)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { 
                "Hồ sơ đang ở trạng thái chờ phụ huynh bổ sung thông tin/giấy tờ. Giáo vụ chỉ có thể trình Ban Giám Hiệu sau khi phụ huynh đã hoàn tất bổ sung." 
            });
        }

        if (req.Status == RequestStatus.UnderReview)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn chuyển lớp này đã được trình Ban Giám Hiệu xem xét, không thể gửi lặp lại." });
        }

        if (req.Status == RequestStatus.Approved || req.Status == RequestStatus.Rejected)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn này đã được hoàn tất xử lý, không thể gửi lại đề xuất." });
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
        if (request.EffectiveDate.HasValue)
        {
            req.ClassTransferDetail.EffectiveDate = request.EffectiveDate.Value;
        }
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

        if (req.RequiresAmendment)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { 
                "Hồ sơ đang ở trạng thái chờ phụ huynh bổ sung thông tin/giấy tờ. Giáo vụ chỉ có thể trình Ban Giám Hiệu sau khi phụ huynh đã hoàn tất bổ sung." 
            });
        }

        if (req.Status == RequestStatus.UnderReview)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn này đã được trình Ban Giám Hiệu xem xét, không thể gửi lặp lại." });
        }

        if (req.Status == RequestStatus.Approved || req.Status == RequestStatus.Rejected)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn này đã được hoàn tất xử lý, không thể gửi lại đề xuất." });
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

    public async Task<ApiResponse<bool>> StaffRejectRequestAsync(
        int requestId,
        int staffUserId,
        StaffRejectDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập lý do từ chối đơn." });
        }

        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn.");
        }

        if (req.Status != RequestStatus.Submitted)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ có thể từ chối đơn khi đang ở trạng thái 'Chờ thẩm định'." });
        }

        req.Status = RequestStatus.Rejected;
        req.ReviewerNotes = request.Reason.Trim();
        req.ReviewerUserId = staffUserId;
        req.ProcessedDate = DateTime.UtcNow;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        await _auditService.LogActionAsync(
            staffUserId,
            AuditActions.AdministrativeRequestReject,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            null,
            new { Reason = request.Reason.Trim(), StaffReject = true },
            null,
            cancellationToken);

        // Notify Parent (MSG32)
        await NotifyGuardiansOfStudentAsync(
            req.StudentId,
            req.RequesterUserId,
            "Yêu cầu hành chính bị từ chối tiếp nhận",
            $"Đơn {req.RequestNumber} ({req.RequestType}) cho học sinh {req.Student?.FullName} không được Giáo vụ tiếp nhận. Lý do: {request.Reason.Trim()}",
            "REQUEST_REJECTED",
            "/parent/requests",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG32);
    }

    public async Task<ApiResponse<bool>> DirectIssueDocumentAsync(
        int requestId,
        int staffUserId,
        StaffDirectIssueDocumentDto request,
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

        if (req.RequiresAmendment)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn đang yêu cầu phụ huynh bổ sung hồ sơ." });
        }

        if (req.Status != RequestStatus.Submitted)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ có thể phát hành trực tiếp các đơn đang ở trạng thái 'Chờ thẩm định'." });
        }

        // Sensitive document types that strictly require School Board approval (Financial/Disciplinary/SchoolTransfer)
        var sensitiveTypesRequiringBoard = new[]
        {
            "Đơn xin chuyển trường",
            "phúc khảo",
            "miễn, giảm",
            "miễn giảm",
            "hỗ trợ chi phí"
        };

        if (sensitiveTypesRequiringBoard.Any(t => req.DocumentRequestDetail.DocumentType.Contains(t, StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> {
                $"Văn bản loại '{req.DocumentRequestDetail.DocumentType}' thuộc thẩm quyền quyết định của Ban Giám Hiệu (chính sách tài chính / chuyên môn khảo thí / chuyển trường). Giáo vụ vui lòng chọn tab 'Trình Ban Giám Hiệu xem xét'."
            });
        }

        var certNum = string.IsNullOrWhiteSpace(request.CertificateNumber)
            ? $"SAWP-{DateTime.UtcNow:yyyy}-" + Guid.NewGuid().ToString("N")[..6].ToUpper()
            : request.CertificateNumber.Trim();

        var issueDate = request.IssuedDate ?? DateTime.UtcNow;

        req.DocumentRequestDetail.CertificateNumber = certNum;
        req.DocumentRequestDetail.IssuedDate = issueDate;
        req.DocumentRequestDetail.IsPaperCopyRequested = request.IsPaperCopy;
        if (!string.IsNullOrWhiteSpace(request.DocumentContent))
        {
            req.DocumentRequestDetail.DocumentContentDraft = request.DocumentContent.Trim();
        }
        req.DocumentRequestDetail.DigitalSignatureInfo = $"Phát hành trực tiếp bởi Văn phòng Giáo vụ ngày {issueDate:dd/MM/yyyy HH:mm}";
        req.DocumentRequestDetail.IssuedDocumentUrl = $"/api/requests/{req.Id}/download-document";

        req.Status = RequestStatus.Completed;
        req.ProcessedDate = issueDate;
        req.ReviewerUserId = staffUserId;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        // Notify parent
        await NotifyGuardiansOfStudentAsync(
            req.StudentId,
            req.RequesterUserId,
            "Giấy xác nhận đã được phát hành",
            $"Văn bản {req.DocumentRequestDetail.DocumentType} cho học sinh {req.Student?.FullName} đã được Giáo vụ xử lý và phát hành chính thức (Số: {certNum}).",
            "DOCUMENT_ISSUED",
            req.DocumentRequestDetail.IssuedDocumentUrl,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG36);
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

        // Check if student is already in target class
        var alreadyInTarget = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .AnyAsync(ca => ca.StudentId == studentId && ca.ClassId == toClassId && ca.IsActive, cancellationToken);

        if (alreadyInTarget)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Học sinh hiện đã ở lớp đích này rồi." });
        }

        // Re-check target class capacity (BR-37/38)
        var currentEnrolledInTarget = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .CountAsync(ca => ca.ClassId == toClassId && ca.IsActive, cancellationToken);

        if (req.ClassTransferDetail.ToClass != null && req.ClassTransferDetail.ToClass.Capacity > 0 && currentEnrolledInTarget >= req.ClassTransferDetail.ToClass.Capacity)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG15, new List<string> { 
                $"Lớp {req.ClassTransferDetail.ToClass.ClassName} đã đủ sĩ số ({currentEnrolledInTarget}/{req.ClassTransferDetail.ToClass.Capacity}). Không thể thực thi chuyển lớp." 
            });
        }

        var effectiveDate = req.ClassTransferDetail.EffectiveDate ?? DateTime.UtcNow;

        // 1. Deactivate ALL active assignments for this student to prevent duplicate active records (BUG2)
        var activeAssignments = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .Where(ca => ca.StudentId == studentId && ca.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var old in activeAssignments)
        {
            old.IsActive = false;
            old.EndDate = effectiveDate;
            await _unitOfWork.Repository<ClassAssignment>().UpdateAsync(old, cancellationToken);
        }

        // 2. Add new active class assignment with effective date
        var newAssignment = new ClassAssignment
        {
            ClassId = toClassId,
            StudentId = studentId,
            StartDate = effectiveDate,
            IsActive = true
        };
        await _unitOfWork.Repository<ClassAssignment>().AddAsync(newAssignment, cancellationToken);

        // 3. Mark ClassTransferDetail as executed & update status to Completed (BUG9)
        req.ClassTransferDetail.IsExecuted = true;
        req.ClassTransferDetail.ExecutedAt = DateTime.UtcNow;
        req.ProcessedDate = DateTime.UtcNow;
        req.Status = RequestStatus.Completed;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        // 4. Send notification with new class details to Parent / Guardians
        await NotifyGuardiansOfStudentAsync(
            req.StudentId,
            req.RequesterUserId,
            "Đã cập nhật chuyển lớp thành công",
            $"Học sinh {req.Student?.FullName} đã được chuyển sang lớp {req.ClassTransferDetail.ToClass?.ClassName} thành công. Vui lòng kiểm tra thời khóa biểu mới của con.",
            "CLASS_TRANSFER_COMPLETED",
            "/parent/children",
            cancellationToken);

        // 5. Audit Log
        await _auditService.LogActionAsync(
            staffUserId,
            AuditActions.ClassTransferRequestExecute,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            new { FromClass = req.ClassTransferDetail.FromClass?.ClassName },
            new { ToClass = req.ClassTransferDetail.ToClass?.ClassName, EffectiveDate = effectiveDate },
            null,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG35);
    }

    #endregion

    #region School Board Operations

    public async Task<ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>> GetSchoolBoardRequestsAsync(
        string? requestType = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.RequesterUser)
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.FromClass)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass).ThenInclude(c => c!.ClassAssignments)
            .Include(r => r.DocumentRequestDetail)
            .Where(r => r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document);

        if (!string.IsNullOrWhiteSpace(requestType) && !requestType.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.RequestType == requestType);
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (status.Equals("UnderReview", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.Status == RequestStatus.UnderReview);
            }
            else if (status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.SchoolBoardApprovedAt != null || r.Status == RequestStatus.Approved);
            }
            else if (status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.Status == RequestStatus.Rejected);
            }
            else if (Enum.TryParse<RequestStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(r => r.Status == parsedStatus);
            }
        }
        else
        {
            query = query.Where(r => r.Status == RequestStatus.UnderReview 
                                  || r.SchoolBoardApprovedAt != null 
                                  || r.Status == RequestStatus.Approved 
                                  || (r.Status == RequestStatus.Rejected && !string.IsNullOrEmpty(r.SchoolBoardNotes)));
        }

        var list = await query
            .OrderByDescending(r => r.SchoolBoardApprovedAt ?? r.ProcessedDate ?? r.SubmissionDate)
            .ToListAsync(cancellationToken);

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
                if (request.EffectiveDate.HasValue && req.ClassTransferDetail != null)
                {
                    req.ClassTransferDetail.EffectiveDate = request.EffectiveDate.Value;
                }

                // BGH approves -> Route back to Office Staff to execute transfer
                req.Status = RequestStatus.Approved;
                req.ReviewerNotes = "BGH đã duyệt đơn chuyển lớp. Đang chờ Giáo vụ thực thi điều chuyển.";

                // Notify Parent (MSG31 - BUG9)
                await NotifyGuardiansOfStudentAsync(
                    req.StudentId,
                    req.RequesterUserId,
                    "BGH đã duyệt đơn chuyển lớp",
                    $"Đơn chuyển lớp {req.RequestNumber} của học sinh {req.Student?.FullName} đã được Ban Giám Hiệu phê duyệt. Giáo vụ sẽ thực hiện điều chuyển lớp trong thời gian sớm nhất.",
                    "REQUEST_APPROVED",
                    "/parent/requests",
                    cancellationToken);

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

                // Send notification to Parent with download link (MSG36)
                await NotifyGuardiansOfStudentAsync(
                    req.StudentId,
                    req.RequesterUserId,
                    "Tài liệu / Giấy xác nhận đã được phát hành",
                    $"Văn bản {req.DocumentRequestDetail.DocumentType} cho học sinh {req.Student?.FullName} đã được Ban Giám Hiệu ký duyệt và phát hành chính thức.",
                    "DOCUMENT_ISSUED",
                    req.DocumentRequestDetail.IssuedDocumentUrl,
                    cancellationToken);
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
                AuditActions.AdministrativeRequestReject,
                nameof(AdministrativeRequest),
                req.Id.ToString(),
                null,
                new { req.RequestType, Reason = request.DecisionNotes.Trim() },
                null, // TODO: pass the client IP address; it is not recorded yet.
                cancellationToken);

            // Notify Parent
            await NotifyGuardiansOfStudentAsync(
                req.StudentId,
                req.RequesterUserId,
                "Yêu cầu hành chính không được phê duyệt",
                $"Yêu cầu {req.RequestNumber} ({req.RequestType}) cho học sinh {req.Student?.FullName} không được BGH phê duyệt. Lý do: {request.DecisionNotes.Trim()}",
                "REQUEST_REJECTED",
                "/parent/requests",
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Ok(true, SystemMessages.MSG32);
        }
    }

    public async Task<ApiResponse<IReadOnlyList<RequestHistoryItemDto>>> GetRequestHistoryAsync(
        int requestId,
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.RequesterUser)
            .Include(r => r.ReviewerUser)
            .Include(r => r.ClassTransferDetail).ThenInclude(ct => ct!.ToClass)
            .Include(r => r.DocumentRequestDetail)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<IReadOnlyList<RequestHistoryItemDto>>.Fail("Không tìm thấy yêu cầu.");
        }

        // Permission check
        var detailCheck = await GetRequestDetailAsync(requestId, currentUserId, cancellationToken);
        if (!detailCheck.Success)
        {
            return ApiResponse<IReadOnlyList<RequestHistoryItemDto>>.Fail(SystemMessages.MSG10);
        }

        var history = new List<RequestHistoryItemDto>();

        // 1. Submitted
        history.Add(new RequestHistoryItemDto
        {
            Timestamp = req.SubmissionDate,
            Action = "Nộp yêu cầu",
            Description = req.RequestType == RequestTypes.ClassTransfer
                ? $"Phụ huynh đã gửi đơn xin chuyển lớp tới {req.ClassTransferDetail?.ToClass?.ClassName ?? "lớp mới"}"
                : $"Phụ huynh đã gửi đơn xin cấp {req.DocumentRequestDetail?.DocumentType ?? "văn bản"}",
            PerformedBy = req.RequesterUser?.FullName ?? "Phụ huynh",
            Status = RequestStatus.Submitted
        });

        // 2. Amendment / Resubmission
        if (req.RequiresAmendment || !string.IsNullOrWhiteSpace(req.AmendmentNotes))
        {
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = req.SubmissionDate.AddMinutes(5),
                Action = "Yêu cầu bổ sung hồ sơ",
                Description = req.AmendmentNotes ?? "Giáo vụ yêu cầu bổ sung thông tin.",
                PerformedBy = req.ReviewerUser?.FullName ?? "Giáo vụ",
                Status = RequestStatus.Submitted
            });
        }

        // 3. Staff endorsement (UnderReview)
        if (req.Status >= RequestStatus.UnderReview && req.Status != RequestStatus.Cancelled)
        {
            var endorseDesc = req.ClassTransferDetail?.StaffRecommendation ?? req.DocumentRequestDetail?.StaffEndorsement;
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = req.SubmissionDate.AddMinutes(15),
                Action = "Giáo vụ thẩm định & chuyển tiếp",
                Description = string.IsNullOrWhiteSpace(endorseDesc) ? "Hồ sơ hợp lệ, đã chuyển Ban Giám Hiệu xem xét." : endorseDesc,
                PerformedBy = req.ReviewerUser?.FullName ?? "Giáo vụ",
                Status = RequestStatus.UnderReview
            });
        }

        // 4. School Board Decision
        if (req.SchoolBoardApprovedAt.HasValue)
        {
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = req.SchoolBoardApprovedAt.Value,
                Action = "Ban Giám Hiệu phê duyệt",
                Description = req.SchoolBoardNotes ?? "Ban Giám Hiệu đồng ý phê duyệt yêu cầu.",
                PerformedBy = "Ban Giám Hiệu",
                Status = RequestStatus.Approved
            });
        }
        else if (req.Status == RequestStatus.Rejected && !string.IsNullOrWhiteSpace(req.SchoolBoardNotes))
        {
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = req.ProcessedDate ?? req.SubmissionDate.AddHours(1),
                Action = "Ban Giám Hiệu từ chối",
                Description = req.SchoolBoardNotes,
                PerformedBy = "Ban Giám Hiệu",
                Status = RequestStatus.Rejected
            });
        }

        // 5. Staff Rejection
        if (req.Status == RequestStatus.Rejected && string.IsNullOrWhiteSpace(req.SchoolBoardNotes))
        {
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = req.ProcessedDate ?? req.SubmissionDate.AddMinutes(30),
                Action = "Giáo vụ từ chối tiếp nhận",
                Description = req.ReviewerNotes ?? "Hồ sơ không hợp lệ.",
                PerformedBy = req.ReviewerUser?.FullName ?? "Giáo vụ",
                Status = RequestStatus.Rejected
            });
        }

        // 6. Cancellation
        if (req.Status == RequestStatus.Cancelled)
        {
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = req.ProcessedDate ?? req.SubmissionDate.AddMinutes(10),
                Action = "Hủy yêu cầu",
                Description = req.ReviewerNotes ?? "Phụ huynh đã chủ động hủy yêu cầu này.",
                PerformedBy = req.RequesterUser?.FullName ?? "Phụ huynh",
                Status = RequestStatus.Cancelled
            });
        }

        // 7. Execution / Issuance (Completed)
        if (req.ClassTransferDetail?.IsExecuted == true)
        {
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = req.ClassTransferDetail.ExecutedAt ?? DateTime.UtcNow,
                Action = "Thực thi chuyển lớp",
                Description = $"Đã cập nhật danh sách lớp cho học sinh sang {req.ClassTransferDetail.ToClass?.ClassName}.",
                PerformedBy = "Giáo vụ",
                Status = RequestStatus.Completed
            });
        }
        else if (req.DocumentRequestDetail?.IssuedDate != null)
        {
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = req.DocumentRequestDetail.IssuedDate.Value,
                Action = "Phát hành văn bản",
                Description = $"Đã phát hành văn bản chính thức số {req.DocumentRequestDetail.CertificateNumber ?? "SAWP"}.",
                PerformedBy = "Văn phòng Nhà trường",
                Status = RequestStatus.Completed
            });
        }

        return ApiResponse<IReadOnlyList<RequestHistoryItemDto>>.Ok(history.OrderBy(h => h.Timestamp).ToList());
    }

    public async Task<ApiResponse<RequestStatisticsDto>> GetRequestStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .ToListAsync(cancellationToken);

        var stats = new RequestStatisticsDto
        {
            TotalRequests = requests.Count,
            SubmittedCount = requests.Count(r => r.Status == RequestStatus.Submitted),
            UnderReviewCount = requests.Count(r => r.Status == RequestStatus.UnderReview),
            ApprovedCount = requests.Count(r => r.Status == RequestStatus.Approved),
            CompletedCount = requests.Count(r => r.Status == RequestStatus.Completed),
            RejectedCount = requests.Count(r => r.Status == RequestStatus.Rejected),
            CancelledCount = requests.Count(r => r.Status == RequestStatus.Cancelled),
            ClassTransferCount = requests.Count(r => r.RequestType == RequestTypes.ClassTransfer),
            DocumentCount = requests.Count(r => r.RequestType == RequestTypes.Document),
            RequiringAmendmentCount = requests.Count(r => r.RequiresAmendment)
        };

        return ApiResponse<RequestStatisticsDto>.Ok(stats);
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
                TargetClassCapacity = (r.ClassTransferDetail.ToClass?.ClassAssignments != null && r.ClassTransferDetail.ToClass.ClassAssignments.Any())
                    ? r.ClassTransferDetail.ToClass.ClassAssignments.Count(ca => ca.IsActive)
                    : r.ClassTransferDetail.TargetClassCapacitySnapshot ?? 0,
                TargetClassMaxCapacity = r.ClassTransferDetail.ToClass?.Capacity,
                IsExecuted = r.ClassTransferDetail.IsExecuted,
                ExecutedAt = r.ClassTransferDetail.ExecutedAt,
                EffectiveDate = r.ClassTransferDetail.EffectiveDate
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
                DigitalSignatureInfo = r.DocumentRequestDetail.DigitalSignatureInfo,
                IsPaperCopyRequested = r.DocumentRequestDetail.IsPaperCopyRequested
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

    private async Task NotifyGuardiansOfStudentAsync(
        int? studentId,
        int fallbackRequesterUserId,
        string title,
        string content,
        string type,
        string linkUrl,
        CancellationToken ct)
    {
        var targetUserIds = new HashSet<int>();

        if (studentId.HasValue && studentId.Value > 0)
        {
            var guardianUserIds = await _unitOfWork.Repository<StudentGuardian>()
                .Query()
                .Include(sg => sg.Guardian)
                .Where(sg => sg.StudentId == studentId.Value && sg.Guardian != null)
                .Select(sg => sg.Guardian.UserId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var gId in guardianUserIds)
            {
                if (gId > 0) targetUserIds.Add(gId);
            }
        }

        if (targetUserIds.Count == 0 && fallbackRequesterUserId > 0)
        {
            targetUserIds.Add(fallbackRequesterUserId);
        }

        foreach (var uId in targetUserIds)
        {
            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                UserId = uId,
                Title = title,
                Content = content,
                Type = type,
                LinkUrl = linkUrl,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            }, ct);
        }
    }

    #endregion
}
