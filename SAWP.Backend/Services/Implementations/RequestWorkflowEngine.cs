using System.Text.Json;
using Core.Common;
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
    private readonly ISchoolClock? _schoolClock;

    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public RequestWorkflowEngine(IUnitOfWork unitOfWork, IAuditService auditService, ISchoolClock? schoolClock = null)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _schoolClock = schoolClock;
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }
        return TimeZoneInfo.CreateCustomTimeZone("SchoolTZ", TimeSpan.FromHours(7), "SchoolTZ", "SchoolTZ");
    }

    private DateTime SchoolNow => _schoolClock?.Now ?? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
    private DateTime SchoolToday => SchoolNow.Date;

    private DateTime ToSchoolDate(DateTime dt) =>
        _schoolClock != null
            ? _schoolClock.ToSchoolTime(dt).Date
            : TimeZoneInfo.ConvertTimeFromUtc(AsUtc(dt), VietnamTimeZone).Date;

    private static DateTime AsUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();

    private static DateTime? AsUtc(DateTime? dt) => dt.HasValue ? AsUtc(dt.Value) : null;

    private static bool ContainsHtmlSyntax(string? input)
        => TextValidationHelper.ContainsUnsafeHtmlOrScript(input);

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

        var transferReasonError = TextValidationHelper.ValidateFreeText(request.TransferReason, "lý do xin chuyển lớp", 5, 1000);
        if (transferReasonError != null)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { transferReasonError });
        }
        var cleanTransferReason = TextValidationHelper.NormalizeWhitespace(request.TransferReason);

        if (request.EffectiveDate.HasValue && request.EffectiveDate.Value.Date < SchoolToday)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Ngày mong muốn chuyển lớp không được là ngày trong quá khứ." });
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

        var schoolNow = SchoolNow;
        var reqNumber = $"REQ-CT-{schoolNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var adminRequest = new AdministrativeRequest
            {
                RequestNumber = reqNumber,
                RequesterUserId = requesterUserId,
                StudentId = request.StudentId,
                RequestType = RequestTypes.ClassTransfer,
                Status = RequestStatus.Submitted,
                AttachmentUrl = request.AttachmentUrl?.Trim(),
                SubmissionDate = DateTime.UtcNow
            };

            await _unitOfWork.Repository<AdministrativeRequest>().AddAsync(adminRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var transferDetail = new ClassTransferDetail
            {
                AdministrativeRequestId = adminRequest.Id,
                FromClassId = currentAssignment.ClassId,
                ToClassId = request.TargetClassId,
                TransferReason = cleanTransferReason,
                EffectiveDate = request.EffectiveDate?.Date,
                TargetClassCapacitySnapshot = targetEnrolled
            };

            await _unitOfWork.Repository<ClassTransferDetail>().AddAsync(transferDetail, cancellationToken);

            await _auditService.LogActionAsync(
                requesterUserId,
                AuditActions.AdministrativeRequestSubmit,
                nameof(AdministrativeRequest),
                adminRequest.Id.ToString(),
                null,
                new
                {
                    RequestNumber = reqNumber,
                    RequestType = RequestTypes.ClassTransfer,
                    ToClass = targetClass.ClassName,
                    Notes = $"Phụ huynh đã gửi đơn xin chuyển lớp tới {targetClass.ClassName}"
                },
                null,
                cancellationToken);

            // Notify Office Staff
            await NotifyStaffMembersAsync(
                "Yêu cầu chuyển lớp mới",
                $"Phụ huynh học sinh {student.FullName} vừa nộp đơn xin chuyển từ lớp {currentAssignment.Class?.ClassName ?? "hiện tại"} sang {targetClass.ClassName}.",
                "/office/requests",
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ApiResponse<string>.Ok(reqNumber, SystemMessages.MSG30);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            return ApiResponse<string>.Fail(SystemMessages.MSG50, new List<string> { "Không thể gửi đơn xin chuyển lớp lúc này. Vui lòng kiểm tra lại thông tin và thử lại." });
        }
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

        var purposeError = TextValidationHelper.ValidateFreeText(request.Purpose, "mục đích xin cấp giấy tờ", 5, 1000);
        if (purposeError != null)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { purposeError });
        }
        var cleanPurpose = TextValidationHelper.NormalizeWhitespace(request.Purpose);

        if (request.NumberOfCopies < 1 || request.NumberOfCopies > 10)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Số lượng bản sao phải từ 1 đến 10 bản (không chấp nhận số 0, số âm hoặc vượt quá 10 bản)." });
        }

        var copies = request.NumberOfCopies;

        if (ContainsHtmlSyntax(request.DocumentType))
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Loại giấy tờ không được chứa thẻ HTML hoặc ký tự '<', '>'." });
        }
        var cleanDocType = System.Text.RegularExpressions.Regex.Replace(request.DocumentType.Trim(), @"\s+", " ");
        if (string.IsNullOrWhiteSpace(cleanDocType) || cleanDocType.Length > 150)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Loại giấy tờ không hợp lệ (tối đa 150 ký tự)." });
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
                        && (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.UnderReview || r.Status == RequestStatus.NeedMoreInfo || r.Status == RequestStatus.ReturnedToStaff)
                        && r.DocumentRequestDetail != null
                        && r.DocumentRequestDetail.DocumentType.ToLower() == cleanDocType.ToLower(),
                        cancellationToken);

        if (hasPendingDocRequest)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG09, new List<string> {
                $"Học sinh hiện đang có đơn xin cấp '{cleanDocType}' đang trong quá trình xử lý. Quý phụ huynh vui lòng chờ kết quả đơn trước đó."
            });
        }

        var reqNumber = $"REQ-DOC-{SchoolNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var adminRequest = new AdministrativeRequest
            {
                RequestNumber = reqNumber,
                RequesterUserId = requesterUserId,
                StudentId = request.StudentId,
                RequestType = RequestTypes.Document,
                Status = RequestStatus.Submitted,
                AttachmentUrl = request.AttachmentUrl?.Trim(),
                SubmissionDate = DateTime.UtcNow
            };

            await _unitOfWork.Repository<AdministrativeRequest>().AddAsync(adminRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var docDetail = new DocumentRequestDetail
            {
                AdministrativeRequestId = adminRequest.Id,
                DocumentType = cleanDocType,
                NumberOfCopies = copies,
                Purpose = cleanPurpose,
                IsPaperCopyRequested = request.IsPaperCopyRequested
            };

            await _unitOfWork.Repository<DocumentRequestDetail>().AddAsync(docDetail, cancellationToken);

            await _auditService.LogActionAsync(
                requesterUserId,
                AuditActions.AdministrativeRequestSubmit,
                nameof(AdministrativeRequest),
                adminRequest.Id.ToString(),
                null,
                new
                {
                    RequestNumber = reqNumber,
                    RequestType = RequestTypes.Document,
                    Detail = cleanDocType,
                    Notes = $"Phụ huynh đã gửi đơn xin cấp {cleanDocType}"
                },
                null,
                cancellationToken);

            // Notify Office Staff
            await NotifyStaffMembersAsync(
                "Yêu cầu cấp giấy tờ / chứng nhận mới",
                $"Phụ huynh học sinh {student.FullName} vừa nộp đơn xin cấp {docDetail.DocumentType} ({docDetail.NumberOfCopies} bản).",
                "/office/requests",
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ApiResponse<string>.Ok(reqNumber, SystemMessages.MSG30);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            return ApiResponse<string>.Fail(SystemMessages.MSG50, new List<string> { "Không thể gửi đơn xin cấp giấy tờ lúc này. Vui lòng kiểm tra lại thông tin và thử lại." });
        }
    }

    public async Task<ApiResponse<string>> SubmitOtherRequestAsync(
        int requesterUserId,
        OtherRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.StudentId <= 0 || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn học sinh, nhập tiêu đề và nội dung đơn." });
        }

        var titleError = TextValidationHelper.ValidateFreeText(request.Title, "tiêu đề loại đơn khác", 5, 150);
        if (titleError != null)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { titleError });
        }
        var cleanTitle = TextValidationHelper.NormalizeWhitespace(request.Title);

        var contentError = TextValidationHelper.ValidateFreeText(request.Content, "nội dung chi tiết đơn", 5, 1000);
        if (contentError != null)
        {
            return ApiResponse<string>.Fail(SystemMessages.MSG01, new List<string> { contentError });
        }
        var cleanContent = TextValidationHelper.NormalizeWhitespace(request.Content);

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

        var reqNumber = $"REQ-OTH-{SchoolNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var adminRequest = new AdministrativeRequest
            {
                RequestNumber = reqNumber,
                RequesterUserId = requesterUserId,
                StudentId = request.StudentId,
                RequestType = RequestTypes.Other,
                Status = RequestStatus.Submitted,
                AttachmentUrl = request.AttachmentUrl?.Trim(),
                SubmissionDate = DateTime.UtcNow
            };

            await _unitOfWork.Repository<AdministrativeRequest>().AddAsync(adminRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var detail = new DocumentRequestDetail
            {
                AdministrativeRequestId = adminRequest.Id,
                DocumentType = cleanTitle,
                NumberOfCopies = 1,
                Purpose = cleanContent,
                IsPaperCopyRequested = false
            };

            await _unitOfWork.Repository<DocumentRequestDetail>().AddAsync(detail, cancellationToken);

            await _auditService.LogActionAsync(
                requesterUserId,
                AuditActions.AdministrativeRequestSubmit,
                nameof(AdministrativeRequest),
                adminRequest.Id.ToString(),
                null,
                new
                {
                    RequestNumber = reqNumber,
                    RequestType = RequestTypes.Other,
                    Detail = cleanTitle,
                    Notes = $"Phụ huynh đã gửi đơn khác: {cleanTitle}"
                },
                null,
                cancellationToken);

            await NotifyStaffMembersAsync(
                "Yêu cầu / Kiến nghị khác mới",
                $"Phụ huynh học sinh {student.FullName} vừa nộp đơn mới: {cleanTitle}.",
                "/office/requests",
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ApiResponse<string>.Ok(reqNumber, SystemMessages.MSG30);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            return ApiResponse<string>.Fail(SystemMessages.MSG50, new List<string> { "Không thể gửi đơn lúc này. Vui lòng kiểm tra lại thông tin và thử lại." });
        }
    }

    public async Task<ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>> GetMyRequestsAsync(
        int parentUserId,
        int? studentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .AsNoTracking()
            .Where(r => r.RequesterUserId == parentUserId
                && (r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document || r.RequestType == RequestTypes.Other));

        if (studentId.HasValue)
        {
            query = query.Where(r => r.StudentId == studentId.Value);
        }

        var dtos = await ProjectToSummaryDto(query.OrderByDescending(r => r.SubmissionDate), includeDraftContent: false)
            .ToListAsync(cancellationToken);
        NormalizeSummaryDates(dtos);
        return ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<PagedAdministrativeRequestsDto>> GetMyRequestsPagedAsync(
        int parentUserId,
        int? studentId = null,
        string? requestType = null,
        string? status = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .AsNoTracking()
            .Where(r => r.RequesterUserId == parentUserId
                && (r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document || r.RequestType == RequestTypes.Other));

        if (studentId.HasValue)
        {
            baseQuery = baseQuery.Where(r => r.StudentId == studentId.Value);
        }

        // Pending flags for selected child across all pages
        var pendingStatuses = new[] { RequestStatus.Submitted, RequestStatus.NeedMoreInfo, RequestStatus.UnderReview, RequestStatus.ReturnedToStaff };
        var pendingTransfer = await baseQuery
            .Where(r => r.RequestType == RequestTypes.ClassTransfer && pendingStatuses.Contains(r.Status))
            .Select(r => r.RequestNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var pendingDocTypes = await baseQuery
            .Where(r => r.RequestType == RequestTypes.Document && pendingStatuses.Contains(r.Status) && r.DocumentRequestDetail != null)
            .Select(r => r.DocumentRequestDetail!.DocumentType)
            .Distinct()
            .ToListAsync(cancellationToken);

        var filtered = baseQuery;
        if (!string.IsNullOrWhiteSpace(requestType) && !requestType.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(r => r.RequestType == requestType);
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<RequestStatus>(status, true, out var st))
        {
            filtered = filtered.Where(r => r.Status == st);
        }

        var (items, totalCount) = await FetchPagedSummariesAsync(
            filtered,
            filtered.OrderByDescending(r => r.SubmissionDate).ThenByDescending(r => r.Id),
            pageIndex,
            pageSize,
            cancellationToken);

        return ApiResponse<PagedAdministrativeRequestsDto>.Ok(new PagedAdministrativeRequestsDto
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
            HasPendingTransfer = pendingTransfer != null,
            PendingTransferRequestNumber = pendingTransfer,
            PendingDocumentTypes = pendingDocTypes
        });
    }

    public async Task<ApiResponse<AdministrativeRequestSummaryDto>> GetRequestDetailAsync(
        int requestId,
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        var reqMeta = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => new { r.Id, r.RequesterUserId, r.StudentId })
            .FirstOrDefaultAsync(cancellationToken);

        if (reqMeta == null)
        {
            return ApiResponse<AdministrativeRequestSummaryDto>.Fail("Không tìm thấy yêu cầu.");
        }

        var hasAccess = await CanAccessRequestAsync(reqMeta.RequesterUserId, reqMeta.StudentId, currentUserId, cancellationToken);
        if (!hasAccess)
        {
            return ApiResponse<AdministrativeRequestSummaryDto>.Fail(SystemMessages.MSG10);
        }

        var dto = await ProjectToSummaryDto(
                _unitOfWork.Repository<AdministrativeRequest>().Query().AsNoTracking().Where(r => r.Id == requestId),
                includeDraftContent: true)
            .FirstOrDefaultAsync(cancellationToken);

        if (dto == null)
        {
            return ApiResponse<AdministrativeRequestSummaryDto>.Fail("Không tìm thấy yêu cầu.");
        }

        NormalizeSummaryDates(dto);
        return ApiResponse<AdministrativeRequestSummaryDto>.Ok(dto);
    }

    private async Task<bool> CanAccessRequestAsync(
        int requesterUserId,
        int? studentId,
        int currentUserId,
        CancellationToken cancellationToken)
    {
        var userRoles = await _unitOfWork.Repository<UserRole>()
            .Query()
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == currentUserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken);

        bool isStaffOrAdmin = userRoles.Any(r =>
            r == UserRoles.OfficeStaff.ToString() ||
            r == UserRoles.SchoolBoard.ToString() ||
            r == UserRoles.Admin.ToString());
        if (isStaffOrAdmin) return true;

        if (requesterUserId == currentUserId || (requesterUserId == 0 && userRoles.Count == 0))
            return true;

        if (studentId.HasValue)
        {
            var isGuardian = await _unitOfWork.Repository<StudentGuardian>()
                .Query()
                .AsNoTracking()
                .Include(sg => sg.Guardian)
                .AnyAsync(sg => sg.StudentId == studentId.Value && sg.Guardian != null && sg.Guardian.UserId == currentUserId, cancellationToken);
            if (isGuardian) return true;

            if (userRoles.Contains("Teacher"))
            {
                var teacherStaff = await _unitOfWork.Repository<StaffProfile>()
                    .Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(sp => sp.UserId == currentUserId, cancellationToken);
                if (teacherStaff != null)
                {
                    var isHomeroomTeacher = await _unitOfWork.Repository<ClassAssignment>()
                        .Query()
                        .AsNoTracking()
                        .Include(ca => ca.Class)
                        .AnyAsync(ca => ca.StudentId == studentId.Value && ca.IsActive && ca.Class.HomeroomTeacherId == teacherStaff.Id, cancellationToken);
                    if (isHomeroomTeacher) return true;
                }
            }
        }

        return false;
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

        if (ContainsHtmlSyntax(request.TransferReason) || ContainsHtmlSyntax(request.Purpose) || ContainsHtmlSyntax(request.AdditionalNotes))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Nội dung chỉnh sửa / bổ sung không được chứa thẻ HTML hoặc mã kịch bản." });
        }

        if (req.ClassTransferDetail != null && !string.IsNullOrWhiteSpace(request.TransferReason))
        {
            var err = TextValidationHelper.ValidateFreeText(request.TransferReason, "lý do xin chuyển lớp", 5, 1000);
            if (err != null)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { err });
            }
            req.ClassTransferDetail.TransferReason = TextValidationHelper.NormalizeWhitespace(request.TransferReason);
        }

        if (req.DocumentRequestDetail != null)
        {
            if (!string.IsNullOrWhiteSpace(request.Purpose))
            {
                var err = TextValidationHelper.ValidateFreeText(request.Purpose, "mục đích xin cấp giấy tờ", 5, 1000);
                if (err != null)
                {
                    return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { err });
                }
                req.DocumentRequestDetail.Purpose = TextValidationHelper.NormalizeWhitespace(request.Purpose);
            }
            if (request.NumberOfCopies.HasValue)
            {
                if (request.NumberOfCopies.Value < 1 || request.NumberOfCopies.Value > 10)
                {
                    return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Số lượng bản sao phải từ 1 đến 10 bản (không chấp nhận số 0, số âm hoặc vượt quá 10 bản)." });
                }
                req.DocumentRequestDetail.NumberOfCopies = request.NumberOfCopies.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.AdditionalNotes))
        {
            var err = TextValidationHelper.ValidateFreeText(request.AdditionalNotes, "ghi chú bổ sung", 5, 1000);
            if (err != null)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { err });
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

        await _auditService.LogActionAsync(
            requesterUserId,
            AuditActions.AdministrativeRequestResubmit,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            null,
            new
            {
                Notes = !string.IsNullOrWhiteSpace(request.AdditionalNotes)
                    ? request.AdditionalNotes.Trim()
                    : "Phụ huynh đã cập nhật thông tin và nộp lại hồ sơ theo yêu cầu bổ sung."
            },
            null,
            cancellationToken);

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
            .AsNoTracking()
            .Where(r => r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document || r.RequestType == RequestTypes.Other);

        if (!string.IsNullOrWhiteSpace(requestType))
        {
            query = query.Where(r => r.RequestType == requestType);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, true, out var st))
        {
            query = query.Where(r => r.Status == st);
        }

        var dtos = await ProjectToSummaryDto(query.OrderByDescending(r => r.SubmissionDate), includeDraftContent: false)
            .ToListAsync(cancellationToken);
        NormalizeSummaryDates(dtos);
        return ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<PagedAdministrativeRequestsDto>> GetOfficeStaffRequestsPagedAsync(
        string? requestType = null,
        string? status = null,
        string? search = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .AsNoTracking()
            .Where(r => r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document || r.RequestType == RequestTypes.Other);

        var statusCounts = await baseQuery
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var pendingExecCount = await baseQuery.CountAsync(
            r => r.Status == RequestStatus.Approved
                && r.RequestType == RequestTypes.ClassTransfer
                && r.ClassTransferDetail != null
                && !r.ClassTransferDetail.IsExecuted,
            cancellationToken);

        var stats = new AdministrativeRequestStatsDto
        {
            TotalCount = statusCounts.Sum(x => x.Count),
            SubmittedCount = statusCounts.FirstOrDefault(x => x.Status == RequestStatus.Submitted)?.Count ?? 0,
            NeedMoreInfoCount = statusCounts.FirstOrDefault(x => x.Status == RequestStatus.NeedMoreInfo)?.Count ?? 0,
            ReturnedToStaffCount = statusCounts.FirstOrDefault(x => x.Status == RequestStatus.ReturnedToStaff)?.Count ?? 0,
            UnderReviewCount = statusCounts.FirstOrDefault(x => x.Status == RequestStatus.UnderReview)?.Count ?? 0,
            ApprovedCount = statusCounts.FirstOrDefault(x => x.Status == RequestStatus.Approved)?.Count ?? 0,
            CompletedCount = statusCounts.FirstOrDefault(x => x.Status == RequestStatus.Completed)?.Count ?? 0,
            RejectedCount = statusCounts.FirstOrDefault(x => x.Status == RequestStatus.Rejected)?.Count ?? 0,
            CancelledCount = statusCounts.FirstOrDefault(x => x.Status == RequestStatus.Cancelled)?.Count ?? 0,
            PendingExecutionCount = pendingExecCount
        };

        var query = baseQuery;
        if (!string.IsNullOrWhiteSpace(requestType) && !requestType.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.RequestType == requestType);
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (status.Equals("AMENDMENT", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.RequiresAmendment);
            }
            else if (status.Equals("PENDING_EXECUTION", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.RequestType == RequestTypes.ClassTransfer && r.SchoolBoardApprovedAt != null && r.ClassTransferDetail != null && !r.ClassTransferDetail.IsExecuted);
            }
            else if (status.Equals("ReturnedToStaff", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.Status == RequestStatus.ReturnedToStaff || r.Status == RequestStatus.NeedMoreInfo);
            }
            else if (Enum.TryParse<RequestStatus>(status, true, out var st))
            {
                query = query.Where(r => r.Status == st && !r.RequiresAmendment);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r =>
                r.RequestNumber.ToLower().Contains(term) ||
                (r.Student != null && (r.Student.FullName.ToLower().Contains(term) || r.Student.StudentCode.ToLower().Contains(term))) ||
                (r.RequesterUser != null && r.RequesterUser.FullName.ToLower().Contains(term)));
        }

        var (items, totalCount) = await FetchPagedSummariesAsync(
            query,
            query.OrderByDescending(r => r.SubmissionDate).ThenByDescending(r => r.Id),
            pageIndex,
            pageSize,
            cancellationToken);

        return ApiResponse<PagedAdministrativeRequestsDto>.Ok(new PagedAdministrativeRequestsDto
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
            Stats = stats
        });
    }

    public async Task<ApiResponse<bool>> RequestAmendmentAsync(
        int requestId,
        int staffUserId,
        StaffAmendmentDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AmendmentNotes) || request.AmendmentNotes.Trim().Length < 5)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập nội dung yêu cầu bổ sung (tối thiểu 5 ký tự)." });
        }
        if (ContainsHtmlSyntax(request.AmendmentNotes))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Nội dung yêu cầu bổ sung không được chứa thẻ HTML hoặc ký tự '<', '>'." });
        }

        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu.");
        }

        if (req.Status != RequestStatus.Submitted && req.Status != RequestStatus.NeedMoreInfo && req.Status != RequestStatus.ReturnedToStaff)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ có thể yêu cầu bổ sung hồ sơ khi đơn đang ở trạng thái 'Chờ thẩm định' hoặc 'BGH yêu cầu chỉnh sửa'." });
        }

        if (req.RequiresAmendment)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn này đã được gửi yêu cầu bổ sung trước đó. Vui lòng đợi phụ huynh hoàn tất và nộp lại hồ sơ." });
        }

        req.RequiresAmendment = true;
        req.AmendmentNotes = request.AmendmentNotes.Trim();
        req.ReviewerUserId = staffUserId;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        await _auditService.LogActionAsync(
            staffUserId,
            AuditActions.AdministrativeRequestAmendment,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            null,
            new { Notes = request.AmendmentNotes.Trim() },
            null,
            cancellationToken);

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

        if (string.IsNullOrWhiteSpace(request.StaffRecommendation) || request.StaffRecommendation.Trim().Length < 5)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập ý kiến thẩm định chuyển lớp (tối thiểu 5 ký tự)." });
        }
        if (ContainsHtmlSyntax(request.StaffRecommendation))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Ý kiến thẩm định không được chứa thẻ HTML hoặc ký tự '<', '>'." });
        }

        var effectiveDate = request.EffectiveDate ?? req.ClassTransferDetail.EffectiveDate;
        if (!effectiveDate.HasValue)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn ngày có hiệu lực chuyển lớp (không được để trống)." });
        }
        if (effectiveDate.Value.Date < SchoolToday)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Ngày có hiệu lực chuyển lớp không được là ngày trong quá khứ." });
        }

        bool isResubmission = req.Status == RequestStatus.ReturnedToStaff || req.Status == RequestStatus.NeedMoreInfo;

        req.ClassTransferDetail.StaffRecommendation = request.StaffRecommendation.Trim();
        req.ClassTransferDetail.TargetClassCapacitySnapshot = currentCount;
        req.ClassTransferDetail.EffectiveDate = effectiveDate.Value.Date;
        req.Status = RequestStatus.UnderReview;
        req.ReviewerUserId = staffUserId;
        req.RequiresAmendment = false;
        req.SchoolBoardNotes = null;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        await _auditService.LogActionAsync(
            staffUserId,
            AuditActions.AdministrativeRequestEndorse,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            null,
            new
            {
                Notes = request.StaffRecommendation.Trim(),
                IsResubmission = isResubmission,
                EffectiveDate = effectiveDate.Value.Date
            },
            null,
            cancellationToken);

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

        if (string.IsNullOrWhiteSpace(request.StaffEndorsement) || request.StaffEndorsement.Trim().Length < 5)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập ghi chú / ý kiến thẩm định của Giáo vụ (tối thiểu 5 ký tự)." });
        }
        if (ContainsHtmlSyntax(request.StaffEndorsement))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Ý kiến thẩm định không được chứa thẻ HTML hoặc ký tự '<', '>'." });
        }

        var existingUploadedUrl = !string.IsNullOrWhiteSpace(req.DocumentRequestDetail.IssuedDocumentUrl)
            && !req.DocumentRequestDetail.IssuedDocumentUrl.Contains("/download-document")
            ? req.DocumentRequestDetail.IssuedDocumentUrl
            : null;
        var effectiveFileUrl = !string.IsNullOrWhiteSpace(request.DocumentFileUrl)
            ? request.DocumentFileUrl.Trim()
            : existingUploadedUrl;

        if (req.RequestType == RequestTypes.Document && string.IsNullOrWhiteSpace(effectiveFileUrl))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng tải lên tệp văn bản (PDF hoặc Ảnh) để Ban Giám Hiệu xem xét và ký số." });
        }

        req.DocumentRequestDetail.StaffEndorsement = request.StaffEndorsement.Trim();
        if (!string.IsNullOrWhiteSpace(effectiveFileUrl))
        {
            req.DocumentRequestDetail.IssuedDocumentUrl = effectiveFileUrl;
        }
        if (!string.IsNullOrWhiteSpace(request.DocumentContentDraft))
        {
            req.DocumentRequestDetail.DocumentContentDraft = request.DocumentContentDraft.Trim();
        }

        bool isResubmission = req.Status == RequestStatus.ReturnedToStaff || req.Status == RequestStatus.NeedMoreInfo;

        req.Status = RequestStatus.UnderReview;
        req.ReviewerUserId = staffUserId;
        req.RequiresAmendment = false;
        req.SchoolBoardNotes = null;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        await _auditService.LogActionAsync(
            staffUserId,
            AuditActions.AdministrativeRequestEndorse,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            null,
            new
            {
                Notes = request.StaffEndorsement.Trim(),
                IsResubmission = isResubmission,
                FileAttached = !string.IsNullOrWhiteSpace(effectiveFileUrl)
            },
            null,
            cancellationToken);

        // Notify SchoolBoard
        await NotifyBoardMembersAsync(
            req.RequestType == RequestTypes.Other ? "Đơn kiến nghị / yêu cầu khác chờ phê duyệt" : "Hồ sơ xin cấp giấy tờ chờ phê duyệt",
            $"Giáo vụ đã thẩm định và trình hồ sơ '{req.DocumentRequestDetail.DocumentType}' cho học sinh {req.Student?.FullName} (Đơn {req.RequestNumber}).",
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
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập lý do từ chối đơn (tối thiểu 5 ký tự)." });
        }
        if (ContainsHtmlSyntax(request.Reason))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Lý do từ chối không được chứa thẻ HTML hoặc ký tự '<', '>'." });
        }

        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn.");
        }

        if (req.Status != RequestStatus.Submitted && req.Status != RequestStatus.NeedMoreInfo && req.Status != RequestStatus.ReturnedToStaff)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ có thể từ chối đơn khi đang ở trạng thái 'Chờ thẩm định' hoặc 'BGH yêu cầu chỉnh sửa'." });
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

        if (req.Status != RequestStatus.Submitted && req.Status != RequestStatus.NeedMoreInfo && req.Status != RequestStatus.ReturnedToStaff)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ có thể phát hành trực tiếp các đơn đang ở trạng thái 'Chờ thẩm định' hoặc 'BGH yêu cầu chỉnh sửa'." });
        }

        // Sensitive document types that strictly require School Board approval (Financial/Disciplinary/SchoolTransfer)
        if (req.RequestType == RequestTypes.Document)
        {
            var sensitiveTypesRequiringBoard = new[]
            {
                "Đơn xin chuyển trường",
                "phúc khảo",
                "miễn, giảm",
                "miễn giảm",
                "hỗ trợ chi phí",
                "bản sao học bạ",
                "hoàn thành chương trình",
                "cấp lại",
                "trích lục hồ sơ"
            };

            if (sensitiveTypesRequiringBoard.Any(t => req.DocumentRequestDetail.DocumentType.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> {
                    $"Văn bản loại '{req.DocumentRequestDetail.DocumentType}' thuộc thẩm quyền quyết định của Ban Giám Hiệu (chính sách tài chính / chuyên môn khảo thí / chuyển trường). Giáo vụ vui lòng chọn tab 'Trình Ban Giám Hiệu xem xét'."
                });
            }
        }

        var schoolNow = SchoolNow;
        var certNum = string.IsNullOrWhiteSpace(request.CertificateNumber)
            ? $"SAWP-{schoolNow:yyyy}-" + Guid.NewGuid().ToString("N")[..6].ToUpper()
            : request.CertificateNumber.Trim();

        var issueDate = request.IssuedDate ?? schoolNow;
        if (issueDate.Date > schoolNow.Date)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Ngày cấp / phản hồi văn bản không được là ngày trong tương lai." });
        }
        if (req.SubmissionDate != default && issueDate.Date < ToSchoolDate(req.SubmissionDate))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Ngày cấp / phản hồi văn bản không thể trước ngày phụ huynh nộp đơn." });
        }

        if (req.RequestType == RequestTypes.Document && string.IsNullOrWhiteSpace(request.DocumentFileUrl))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng tải lên tệp văn bản đã xác nhận/đóng dấu (PDF hoặc Ảnh) để phát hành cho Phụ huynh." });
        }

        if (req.RequestType == RequestTypes.Other
            && string.IsNullOrWhiteSpace(request.DocumentFileUrl)
            && (string.IsNullOrWhiteSpace(request.StaffNotes) || request.StaffNotes.Trim().Length < 5))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập ghi chú phản hồi (tối thiểu 5 ký tự) hoặc đính kèm tệp kết quả." });
        }

        req.DocumentRequestDetail.CertificateNumber = certNum;
        req.DocumentRequestDetail.IssuedDate = DateTime.UtcNow;
        req.DocumentRequestDetail.IsPaperCopyRequested = request.IsPaperCopy;
        if (!string.IsNullOrWhiteSpace(request.DocumentContent))
        {
            req.DocumentRequestDetail.DocumentContentDraft = request.DocumentContent.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.StaffNotes))
        {
            req.DocumentRequestDetail.StaffEndorsement = request.StaffNotes.Trim();
            req.ReviewerNotes = request.StaffNotes.Trim();
        }
        req.DocumentRequestDetail.DigitalSignatureInfo = req.RequestType == RequestTypes.Other
            ? $"Đã giải quyết bởi Văn phòng Giáo vụ ngày {issueDate:dd/MM/yyyy} {schoolNow:HH:mm}"
            : $"Phát hành trực tiếp bởi Văn phòng Giáo vụ ngày {issueDate:dd/MM/yyyy} {schoolNow:HH:mm}";
        req.DocumentRequestDetail.IssuedDocumentUrl = !string.IsNullOrWhiteSpace(request.DocumentFileUrl)
            ? request.DocumentFileUrl.Trim()
            : null;

        req.Status = RequestStatus.Completed;
        req.ProcessedDate = DateTime.UtcNow;
        req.ReviewerUserId = staffUserId;
        req.SchoolBoardNotes = null;

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

        await _auditService.LogActionAsync(
            staffUserId,
            AuditActions.AdministrativeRequestDirectIssue,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            null,
            new
            {
                CertificateNumber = certNum,
                Notes = !string.IsNullOrWhiteSpace(request.StaffNotes)
                    ? request.StaffNotes.Trim()
                    : $"Đã phát hành trực tiếp văn bản số {certNum}"
            },
            null,
            cancellationToken);

        // Notify parent
        var isOther = req.RequestType == RequestTypes.Other;
        await NotifyGuardiansOfStudentAsync(
            req.StudentId,
            req.RequesterUserId,
            isOther ? "Đơn đề xuất / kiến nghị đã được giải quyết" : "Giấy xác nhận đã được phát hành",
            isOther
                ? $"Đơn \"{req.DocumentRequestDetail.DocumentType}\" cho học sinh {req.Student?.FullName} đã được Giáo vụ xử lý và giải quyết (Mã: {certNum})."
                : $"Văn bản {req.DocumentRequestDetail.DocumentType} cho học sinh {req.Student?.FullName} đã được Giáo vụ xử lý và phát hành chính thức (Số: {certNum}).",
            "DOCUMENT_ISSUED",
            req.DocumentRequestDetail.IssuedDocumentUrl ?? "/parent/requests",
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

        var effectiveDate = (req.ClassTransferDetail.EffectiveDate ?? SchoolToday).Date;

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
            .AsNoTracking()
            .Where(r => r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document || r.RequestType == RequestTypes.Other);

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
                                  || r.Status == RequestStatus.ReturnedToStaff
                                  || (r.Status == RequestStatus.NeedMoreInfo && !string.IsNullOrEmpty(r.SchoolBoardNotes))
                                  || (r.Status == RequestStatus.Rejected && !string.IsNullOrEmpty(r.SchoolBoardNotes)));
        }

        var ordered = query.OrderByDescending(r => r.SchoolBoardApprovedAt ?? r.ProcessedDate ?? r.SubmissionDate);
        var dtos = await ProjectToSummaryDto(ordered, includeDraftContent: false).ToListAsync(cancellationToken);
        NormalizeSummaryDates(dtos);
        return ApiResponse<IReadOnlyList<AdministrativeRequestSummaryDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<PagedAdministrativeRequestsDto>> GetSchoolBoardRequestsPagedAsync(
        string? requestType = null,
        string? status = null,
        string? search = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var boardBase = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .AsNoTracking()
            .Where(r => (r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document || r.RequestType == RequestTypes.Other)
                     && (r.Status == RequestStatus.UnderReview
                         || r.SchoolBoardApprovedAt != null
                         || r.Status == RequestStatus.Approved
                         || r.Status == RequestStatus.ReturnedToStaff
                         || (r.Status == RequestStatus.NeedMoreInfo && !string.IsNullOrEmpty(r.SchoolBoardNotes))
                         || (r.Status == RequestStatus.Rejected && !string.IsNullOrEmpty(r.SchoolBoardNotes))));

        var boardGroups = await boardBase
            .GroupBy(r => new
            {
                r.Status,
                HasApprovedAt = r.SchoolBoardApprovedAt != null,
                HasBoardNotes = r.SchoolBoardNotes != null && r.SchoolBoardNotes != ""
            })
            .Select(g => new { g.Key.Status, g.Key.HasApprovedAt, g.Key.HasBoardNotes, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var stats = new AdministrativeRequestStatsDto
        {
            TotalCount = boardGroups.Sum(x => x.Count),
            UnderReviewCount = boardGroups.Where(x => x.Status == RequestStatus.UnderReview).Sum(x => x.Count),
            ReturnedToStaffCount = boardGroups.Where(x => x.Status == RequestStatus.ReturnedToStaff || (x.Status == RequestStatus.NeedMoreInfo && x.HasBoardNotes)).Sum(x => x.Count),
            ApprovedCount = boardGroups.Where(x => x.HasApprovedAt || x.Status == RequestStatus.Approved).Sum(x => x.Count),
            RejectedCount = boardGroups.Where(x => x.Status == RequestStatus.Rejected).Sum(x => x.Count)
        };

        var query = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .AsNoTracking()
            .Where(r => r.RequestType == RequestTypes.ClassTransfer || r.RequestType == RequestTypes.Document || r.RequestType == RequestTypes.Other);

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
            else if (status.Equals("ReturnedToStaff", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.Status == RequestStatus.ReturnedToStaff || (r.Status == RequestStatus.NeedMoreInfo && !string.IsNullOrEmpty(r.SchoolBoardNotes)));
            }
            else if (status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.SchoolBoardApprovedAt != null || r.Status == RequestStatus.Approved);
            }
            else if (status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.Status == RequestStatus.Rejected && !string.IsNullOrEmpty(r.SchoolBoardNotes));
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
                                  || r.Status == RequestStatus.ReturnedToStaff
                                  || (r.Status == RequestStatus.NeedMoreInfo && !string.IsNullOrEmpty(r.SchoolBoardNotes))
                                  || (r.Status == RequestStatus.Rejected && !string.IsNullOrEmpty(r.SchoolBoardNotes)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r =>
                r.RequestNumber.ToLower().Contains(term) ||
                (r.Student != null && (r.Student.FullName.ToLower().Contains(term) || r.Student.StudentCode.ToLower().Contains(term))) ||
                (r.RequesterUser != null && r.RequesterUser.FullName.ToLower().Contains(term)));
        }

        var (items, totalCount) = await FetchPagedSummariesAsync(
            query,
            query.OrderByDescending(r => r.SchoolBoardApprovedAt ?? r.ProcessedDate ?? r.SubmissionDate).ThenByDescending(r => r.Id),
            pageIndex,
            pageSize,
            cancellationToken);

        return ApiResponse<PagedAdministrativeRequestsDto>.Ok(new PagedAdministrativeRequestsDto
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
            Stats = stats
        });
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

        if (ContainsHtmlSyntax(request.DecisionNotes))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Ghi chú / Lý do quyết định không được chứa thẻ HTML hoặc ký tự '<', '>'." });
        }

        // NEW FLOW: School Board returns the request to Office Staff for revision / correction (Document & Other only)
        if (request.ReturnToStaff)
        {
            if (req.RequestType == RequestTypes.ClassTransfer)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đơn xin chuyển lớp chỉ hỗ trợ Phê duyệt (có thể điều chỉnh ngày hiệu lực trực tiếp) hoặc Từ chối, không áp dụng trả về Giáo vụ trình lại." });
            }

            if (string.IsNullOrWhiteSpace(request.DecisionNotes) || request.DecisionNotes.Trim().Length < 5)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập nội dung yêu cầu Giáo vụ chỉnh sửa / gửi lại (tối thiểu 5 ký tự)." });
            }

            req.Status = RequestStatus.ReturnedToStaff;
            req.SchoolBoardNotes = request.DecisionNotes.Trim();
            req.ReviewerUserId = boardUserId;

            await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

            await _auditService.LogActionAsync(
                boardUserId,
                AuditActions.AdministrativeRequestBoardReturn,
                nameof(AdministrativeRequest),
                req.Id.ToString(),
                new { OldStatus = RequestStatus.UnderReview },
                new { NewStatus = RequestStatus.ReturnedToStaff, Notes = request.DecisionNotes.Trim() },
                null,
                cancellationToken);

            await NotifyStaffMembersAsync(
                "BGH yêu cầu chỉnh sửa & trình lại hồ sơ",
                $"Ban Giám Hiệu trả lại đơn {req.RequestNumber} (học sinh {req.Student?.FullName}) cho Giáo vụ rà soát: {request.DecisionNotes.Trim()}",
                "/office/requests",
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Ok(true, "Đã gửi yêu cầu chỉnh sửa và trả hồ sơ về cho bộ phận Giáo vụ.");
        }

        if (request.IsApproved)
        {
            if (req.RequestType == RequestTypes.ClassTransfer && request.EffectiveDate.HasValue && request.EffectiveDate.Value.Date < SchoolToday)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Ngày có hiệu lực chuyển lớp không được là ngày trong quá khứ." });
            }

            var schoolNow = SchoolNow;
            req.SchoolBoardApprovedAt = DateTime.UtcNow;
            req.SchoolBoardNotes = request.DecisionNotes?.Trim();
            req.ReviewerUserId = boardUserId;

            if (req.RequestType == RequestTypes.ClassTransfer)
            {
                if (request.EffectiveDate.HasValue && req.ClassTransferDetail != null)
                {
                    req.ClassTransferDetail.EffectiveDate = request.EffectiveDate.Value.Date;
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
            else if ((req.RequestType == RequestTypes.Document || req.RequestType == RequestTypes.Other) && req.DocumentRequestDetail != null)
            {
                if (req.RequestType == RequestTypes.Document && string.IsNullOrWhiteSpace(request.SignedDocumentUrl))
                {
                    return ApiResponse<bool>.Fail(
                        SystemMessages.MSG01,
                        new List<string> { "Vui lòng tải lên tệp văn bản đã gắn chữ ký số / đóng dấu (PDF hoặc Ảnh) trước khi phê duyệt phát hành." });
                }

                // BGH approves -> attach digitally signed file (required for Document, optional for Other) and issue/complete
                var certNum = string.IsNullOrWhiteSpace(req.DocumentRequestDetail.CertificateNumber)
                    ? $"SAWP-{schoolNow:yyyy}-" + Guid.NewGuid().ToString("N")[..6].ToUpper()
                    : req.DocumentRequestDetail.CertificateNumber;
                req.DocumentRequestDetail.CertificateNumber = certNum;
                req.DocumentRequestDetail.DigitalSignatureInfo = req.RequestType == RequestTypes.Other
                    ? $"Đã phê duyệt bởi Ban Giám Hiệu ngày {schoolNow:dd/MM/yyyy HH:mm}"
                    : $"Đã ký số và phát hành bởi Ban Giám Hiệu ngày {schoolNow:dd/MM/yyyy HH:mm}";
                req.DocumentRequestDetail.IssuedDate = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(request.SignedDocumentUrl))
                {
                    req.DocumentRequestDetail.IssuedDocumentUrl = request.SignedDocumentUrl.Trim();
                }
                else if (req.RequestType == RequestTypes.Other
                    && req.DocumentRequestDetail.IssuedDocumentUrl != null
                    && req.DocumentRequestDetail.IssuedDocumentUrl.Contains("/download-document"))
                {
                    req.DocumentRequestDetail.IssuedDocumentUrl = null;
                }

                req.Status = RequestStatus.Approved;
                req.ProcessedDate = DateTime.UtcNow;

                var isOther = req.RequestType == RequestTypes.Other;
                await NotifyGuardiansOfStudentAsync(
                    req.StudentId,
                    req.RequesterUserId,
                    isOther ? "Đơn yêu cầu đã được Ban Giám Hiệu phê duyệt" : "Tài liệu / Giấy xác nhận đã được phát hành",
                    isOther
                        ? $"Đơn '{req.DocumentRequestDetail.DocumentType}' cho học sinh {req.Student?.FullName} đã được Ban Giám Hiệu phê duyệt."
                        : $"Văn bản {req.DocumentRequestDetail.DocumentType} cho học sinh {req.Student?.FullName} đã được Ban Giám Hiệu ký duyệt và phát hành chính thức.",
                    isOther ? "REQUEST_APPROVED" : "DOCUMENT_ISSUED",
                    "/parent/requests",
                    cancellationToken);

                if (isOther)
                {
                    await NotifyStaffMembersAsync(
                        "BGH đã phê duyệt đơn khác",
                        $"Ban Giám Hiệu đã phê duyệt đơn '{req.DocumentRequestDetail.DocumentType}' ({req.RequestNumber}) của học sinh {req.Student?.FullName}.",
                        "/office/requests",
                        cancellationToken);
                }
            }

            await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(req, cancellationToken);

            await _auditService.LogActionAsync(
                boardUserId,
                AuditActions.AdministrativeRequestApprove,
                nameof(AdministrativeRequest),
                req.Id.ToString(),
                new { OldStatus = RequestStatus.UnderReview },
                new
                {
                    NewStatus = RequestStatus.Approved,
                    Notes = string.IsNullOrWhiteSpace(req.SchoolBoardNotes)
                        ? "Ban Giám Hiệu đồng ý phê duyệt yêu cầu."
                        : req.SchoolBoardNotes,
                    CertificateNumber = req.DocumentRequestDetail?.CertificateNumber
                },
                null,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Ok(true, SystemMessages.MSG31);
        }
        else
        {
            // Rejection requires decision notes (>= 5 chars)
            if (string.IsNullOrWhiteSpace(request.DecisionNotes) || request.DecisionNotes.Trim().Length < 5)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập lý do từ chối yêu cầu (tối thiểu 5 ký tự)." });
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
            .AsNoTracking()
            .AsSplitQuery()
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
        var hasAccess = await CanAccessRequestAsync(req.RequesterUserId, req.StudentId, currentUserId, cancellationToken);
        if (!hasAccess)
        {
            return ApiResponse<IReadOnlyList<RequestHistoryItemDto>>.Fail(SystemMessages.MSG10);
        }

        var auditRepo = _unitOfWork.Repository<AuditLog>();
        var auditLogs = auditRepo != null
            ? await auditRepo.Query()
                .AsNoTracking()
                .Include(al => al.User)
                .Where(al => al.EntityName == nameof(AdministrativeRequest) && al.EntityId == requestId.ToString())
                .OrderBy(al => al.Timestamp)
                .ThenBy(al => al.Id)
                .ToListAsync(cancellationToken)
            : new List<AuditLog>();

        if (auditLogs.Count > 0)
        {
            var auditHistory = new List<RequestHistoryItemDto>();

            if (!auditLogs.Any(l => l.Action == AuditActions.AdministrativeRequestSubmit))
            {
                auditHistory.Add(BuildSubmitHistoryItem(req));
            }

            foreach (var log in auditLogs)
            {
                var mapped = MapAuditLogToHistoryItem(log, req);
                if (mapped != null)
                {
                    auditHistory.Add(mapped);
                }
            }

            return ApiResponse<IReadOnlyList<RequestHistoryItemDto>>.Ok(
                auditHistory.OrderBy(h => h.Timestamp).ToList());
        }

        var history = new List<RequestHistoryItemDto>();

        // 1. Submitted
        history.Add(BuildSubmitHistoryItem(req));

        // 2. Amendment / Resubmission
        if (req.RequiresAmendment || !string.IsNullOrWhiteSpace(req.AmendmentNotes))
        {
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = AsUtc(req.SubmissionDate.AddMinutes(5)),
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
                Timestamp = AsUtc(req.SubmissionDate.AddMinutes(15)),
                Action = "Giáo vụ thẩm định & chuyển tiếp",
                Description = string.IsNullOrWhiteSpace(endorseDesc) ? "Hồ sơ hợp lệ, đã chuyển Ban Giám Hiệu xem xét." : endorseDesc,
                PerformedBy = req.ReviewerUser?.FullName ?? "Giáo vụ",
                Status = RequestStatus.UnderReview
            });
        }

        // 3b. School Board returned to Staff for revision
        if ((req.Status == RequestStatus.ReturnedToStaff || req.Status == RequestStatus.NeedMoreInfo) && !string.IsNullOrWhiteSpace(req.SchoolBoardNotes))
        {
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = AsUtc(req.SubmissionDate.AddMinutes(25)),
                Action = "Ban Giám Hiệu yêu cầu Giáo vụ trình lại",
                Description = req.SchoolBoardNotes,
                PerformedBy = "Ban Giám Hiệu",
                Status = RequestStatus.ReturnedToStaff
            });
        }

        // 4. School Board Decision
        if (req.SchoolBoardApprovedAt.HasValue)
        {
            history.Add(new RequestHistoryItemDto
            {
                Timestamp = AsUtc(req.SchoolBoardApprovedAt.Value),
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
                Timestamp = AsUtc(req.ProcessedDate ?? req.SubmissionDate.AddHours(1)),
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
                Timestamp = AsUtc(req.ProcessedDate ?? req.SubmissionDate.AddMinutes(30)),
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
                Timestamp = AsUtc(req.ProcessedDate ?? req.SubmissionDate.AddMinutes(10)),
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
                Timestamp = AsUtc(req.ClassTransferDetail.ExecutedAt ?? DateTime.UtcNow),
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
                Timestamp = AsUtc(req.DocumentRequestDetail.IssuedDate.Value),
                Action = "Phát hành văn bản",
                Description = $"Đã phát hành văn bản chính thức số {req.DocumentRequestDetail.CertificateNumber ?? "SAWP"}.",
                PerformedBy = "Văn phòng Nhà trường",
                Status = RequestStatus.Completed
            });
        }

        return ApiResponse<IReadOnlyList<RequestHistoryItemDto>>.Ok(history.OrderBy(h => h.Timestamp).ToList());
    }

    private static RequestHistoryItemDto BuildSubmitHistoryItem(AdministrativeRequest req) => new()
    {
        Timestamp = AsUtc(req.SubmissionDate),
        Action = "Nộp yêu cầu",
        Description = req.RequestType == RequestTypes.ClassTransfer
            ? $"Phụ huynh đã gửi đơn xin chuyển lớp tới {req.ClassTransferDetail?.ToClass?.ClassName ?? "lớp mới"}"
            : req.RequestType == RequestTypes.Other
                ? $"Phụ huynh đã gửi đơn khác: {req.DocumentRequestDetail?.DocumentType ?? "Kiến nghị / Yêu cầu khác"}"
                : $"Phụ huynh đã gửi đơn xin cấp {req.DocumentRequestDetail?.DocumentType ?? "văn bản"}",
        PerformedBy = req.RequesterUser?.FullName ?? "Phụ huynh",
        Status = RequestStatus.Submitted
    };

    private static RequestHistoryItemDto? MapAuditLogToHistoryItem(AuditLog log, AdministrativeRequest req)
    {
        string? notes = null;
        string? reason = null;
        string? certNum = null;
        string? toClass = null;
        bool isResubmission = false;
        bool isStaffReject = false;

        if (!string.IsNullOrWhiteSpace(log.NewValues))
        {
            try
            {
                using var doc = JsonDocument.Parse(log.NewValues);
                var root = doc.RootElement;
                if (root.TryGetProperty("Notes", out var p) && p.ValueKind == JsonValueKind.String) notes = p.GetString();
                if (root.TryGetProperty("Reason", out var r) && r.ValueKind == JsonValueKind.String) reason = r.GetString();
                if (root.TryGetProperty("CertificateNumber", out var c) && c.ValueKind == JsonValueKind.String) certNum = c.GetString();
                if (root.TryGetProperty("ToClass", out var tc) && tc.ValueKind == JsonValueKind.String) toClass = tc.GetString();
                if (root.TryGetProperty("IsResubmission", out var ir) && (ir.ValueKind == JsonValueKind.True || ir.ValueKind == JsonValueKind.False)) isResubmission = ir.GetBoolean();
                if (root.TryGetProperty("StaffReject", out var sr) && (sr.ValueKind == JsonValueKind.True || sr.ValueKind == JsonValueKind.False)) isStaffReject = sr.GetBoolean();
            }
            catch
            {
                // Ignore malformed JSON in legacy audit entries
            }
        }

        var actor = log.User?.FullName;
        var ts = AsUtc(log.Timestamp);

        return log.Action switch
        {
            AuditActions.AdministrativeRequestSubmit => new RequestHistoryItemDto
            {
                Timestamp = ts,
                Action = "Nộp yêu cầu",
                Description = notes ?? BuildSubmitHistoryItem(req).Description,
                PerformedBy = actor ?? req.RequesterUser?.FullName ?? "Phụ huynh",
                Status = RequestStatus.Submitted
            },
            AuditActions.AdministrativeRequestAmendment => new RequestHistoryItemDto
            {
                Timestamp = ts,
                Action = "Giáo vụ yêu cầu bổ sung hồ sơ",
                Description = notes ?? "Giáo vụ yêu cầu phụ huynh bổ sung thông tin.",
                PerformedBy = actor ?? "Giáo vụ",
                Status = RequestStatus.NeedMoreInfo
            },
            AuditActions.AdministrativeRequestResubmit => new RequestHistoryItemDto
            {
                Timestamp = ts,
                Action = "Phụ huynh cập nhật & nộp lại hồ sơ",
                Description = notes ?? "Phụ huynh đã bổ sung và gửi lại hồ sơ.",
                PerformedBy = actor ?? req.RequesterUser?.FullName ?? "Phụ huynh",
                Status = RequestStatus.Submitted
            },
            AuditActions.AdministrativeRequestEndorse => new RequestHistoryItemDto
            {
                Timestamp = ts,
                Action = isResubmission ? "Giáo vụ chỉnh sửa & trình lại BGH" : "Giáo vụ thẩm định & chuyển tiếp",
                Description = notes ?? "Hồ sơ hợp lệ, đã chuyển Ban Giám Hiệu xem xét.",
                PerformedBy = actor ?? "Giáo vụ",
                Status = RequestStatus.UnderReview
            },
            AuditActions.AdministrativeRequestBoardReturn or "BoardReturnToStaff" => new RequestHistoryItemDto
            {
                Timestamp = ts,
                Action = "Ban Giám Hiệu yêu cầu Giáo vụ trình lại",
                Description = notes ?? "Ban Giám Hiệu yêu cầu Giáo vụ rà soát và trình lại hồ sơ.",
                PerformedBy = actor ?? "Ban Giám Hiệu",
                Status = RequestStatus.ReturnedToStaff
            },
            AuditActions.AdministrativeRequestApprove => new RequestHistoryItemDto
            {
                Timestamp = ts,
                Action = "Ban Giám Hiệu phê duyệt",
                Description = string.IsNullOrWhiteSpace(certNum)
                    ? (notes ?? "Ban Giám Hiệu đồng ý phê duyệt yêu cầu.")
                    : $"{notes ?? "Ban Giám Hiệu đồng ý phê duyệt."} (Số hiệu: {certNum})",
                PerformedBy = actor ?? "Ban Giám Hiệu",
                Status = RequestStatus.Approved
            },
            AuditActions.AdministrativeRequestDirectIssue => new RequestHistoryItemDto
            {
                Timestamp = ts,
                Action = req.RequestType == RequestTypes.Other ? "Giáo vụ xử lý trực tiếp" : "Giáo vụ phát hành trực tiếp",
                Description = string.IsNullOrWhiteSpace(certNum)
                    ? (notes ?? "Giáo vụ đã xử lý và hoàn tất yêu cầu.")
                    : $"{notes ?? "Đã phát hành văn bản"} (Số: {certNum})",
                PerformedBy = actor ?? "Giáo vụ",
                Status = RequestStatus.Completed
            },
            AuditActions.ClassTransferRequestExecute => new RequestHistoryItemDto
            {
                Timestamp = ts,
                Action = "Thực thi chuyển lớp",
                Description = $"Đã cập nhật danh sách lớp cho học sinh sang {toClass ?? req.ClassTransferDetail?.ToClass?.ClassName ?? "lớp mới"}.",
                PerformedBy = actor ?? "Giáo vụ",
                Status = RequestStatus.Completed
            },
            AuditActions.AdministrativeRequestReject => new RequestHistoryItemDto
            {
                Timestamp = ts,
                Action = isStaffReject ? "Giáo vụ từ chối tiếp nhận" : "Ban Giám Hiệu từ chối",
                Description = reason ?? notes ?? "Yêu cầu không được chấp thuận.",
                PerformedBy = actor ?? (isStaffReject ? "Giáo vụ" : "Ban Giám Hiệu"),
                Status = RequestStatus.Rejected
            },
            AuditActions.AdministrativeRequestCancel or "CancelRequest" => new RequestHistoryItemDto
            {
                Timestamp = ts,
                Action = "Hủy yêu cầu",
                Description = notes ?? "Phụ huynh đã chủ động hủy yêu cầu này.",
                PerformedBy = actor ?? req.RequesterUser?.FullName ?? "Phụ huynh",
                Status = RequestStatus.Cancelled
            },
            _ => null
        };
    }

    public async Task<ApiResponse<RequestStatisticsDto>> GetRequestStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .AsNoTracking()
            .Select(r => new { r.Status, r.RequestType, r.RequiresAmendment })
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

    private async Task<(List<AdministrativeRequestSummaryDto> Items, int TotalCount)> FetchPagedSummariesAsync(
        IQueryable<AdministrativeRequest> filteredQuery,
        IQueryable<AdministrativeRequest> orderedQuery,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await filteredQuery.CountAsync(cancellationToken);
        if (totalCount == 0 || (pageIndex - 1) * pageSize >= totalCount)
        {
            return (new List<AdministrativeRequestSummaryDto>(), totalCount);
        }

        var pageIds = await orderedQuery
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (pageIds.Count == 0)
        {
            return (new List<AdministrativeRequestSummaryDto>(), totalCount);
        }

        // Scalar projection avoids N+1 correlated ClassAssignments subqueries
        var rows = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .AsNoTracking()
            .Where(r => pageIds.Contains(r.Id))
            .Select(r => new AdministrativeRequestSummaryDto
            {
                Id = r.Id,
                RequestNumber = r.RequestNumber,
                RequestType = r.RequestType,
                Status = r.Status,
                SubmissionDate = r.SubmissionDate,
                ProcessedDate = r.ProcessedDate,
                RequesterUserId = r.RequesterUserId,
                RequesterName = r.RequesterUser != null ? r.RequesterUser.FullName : "Phụ huynh",
                StudentId = r.StudentId,
                StudentCode = r.Student != null ? r.Student.StudentCode : null,
                StudentName = r.Student != null ? r.Student.FullName : null,
                CurrentClassName = r.ClassTransferDetail != null && r.ClassTransferDetail.FromClass != null
                    ? r.ClassTransferDetail.FromClass.ClassName
                    : null,
                RequiresAmendment = r.RequiresAmendment,
                AttachmentUrl = r.AttachmentUrl,
                AmendmentNotes = r.AmendmentNotes,
                ReviewerNotes = r.ReviewerNotes,
                SchoolBoardNotes = r.SchoolBoardNotes,
                SchoolBoardApprovedAt = r.SchoolBoardApprovedAt,
                ClassTransfer = r.ClassTransferDetail == null ? null : new ClassTransferSummaryDto
                {
                    FromClassId = r.ClassTransferDetail.FromClassId,
                    FromClassName = r.ClassTransferDetail.FromClass != null ? r.ClassTransferDetail.FromClass.ClassName : string.Empty,
                    ToClassId = r.ClassTransferDetail.ToClassId,
                    ToClassName = r.ClassTransferDetail.ToClass != null ? r.ClassTransferDetail.ToClass.ClassName : string.Empty,
                    TransferReason = r.ClassTransferDetail.TransferReason,
                    StaffRecommendation = r.ClassTransferDetail.StaffRecommendation,
                    TargetClassCapacity = r.ClassTransferDetail.TargetClassCapacitySnapshot ?? 0,
                    TargetClassMaxCapacity = r.ClassTransferDetail.ToClass != null ? r.ClassTransferDetail.ToClass.Capacity : null,
                    IsExecuted = r.ClassTransferDetail.IsExecuted,
                    ExecutedAt = r.ClassTransferDetail.ExecutedAt,
                    EffectiveDate = r.ClassTransferDetail.EffectiveDate
                },
                DocumentRequest = r.DocumentRequestDetail == null ? null : new DocumentRequestSummaryDto
                {
                    DocumentType = r.DocumentRequestDetail.DocumentType,
                    NumberOfCopies = r.DocumentRequestDetail.NumberOfCopies,
                    Purpose = r.DocumentRequestDetail.Purpose,
                    StaffEndorsement = r.DocumentRequestDetail.StaffEndorsement,
                    DocumentContentDraft = null,
                    IssuedDate = r.DocumentRequestDetail.IssuedDate,
                    IssuedDocumentUrl = r.DocumentRequestDetail.IssuedDocumentUrl,
                    CertificateNumber = r.DocumentRequestDetail.CertificateNumber,
                    DigitalSignatureInfo = r.DocumentRequestDetail.DigitalSignatureInfo,
                    IsPaperCopyRequested = r.DocumentRequestDetail.IsPaperCopyRequested
                }
            })
            .ToListAsync(cancellationToken);

        var missingClassStudentIds = rows
            .Where(r => r.CurrentClassName == null && r.StudentId.HasValue)
            .Select(r => r.StudentId!.Value)
            .Distinct()
            .ToList();

        Dictionary<int, string> studentClassMap = new();
        if (missingClassStudentIds.Count > 0)
        {
            studentClassMap = (await _unitOfWork.Repository<ClassAssignment>()
                .Query()
                .AsNoTracking()
                .Where(ca => ca.IsActive && missingClassStudentIds.Contains(ca.StudentId))
                .Select(ca => new { ca.StudentId, ClassName = ca.Class.ClassName })
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.StudentId)
                .ToDictionary(g => g.Key, g => g.First().ClassName);
        }

        var toClassIds = rows
            .Where(r => r.ClassTransfer != null && r.ClassTransfer.ToClassId > 0)
            .Select(r => r.ClassTransfer!.ToClassId)
            .Distinct()
            .ToList();

        Dictionary<int, int> activeClassCounts = new();
        if (toClassIds.Count > 0)
        {
            activeClassCounts = (await _unitOfWork.Repository<ClassAssignment>()
                .Query()
                .AsNoTracking()
                .Where(ca => ca.IsActive && toClassIds.Contains(ca.ClassId))
                .GroupBy(ca => ca.ClassId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.ClassId, x => x.Count);
        }

        foreach (var item in rows)
        {
            if (item.CurrentClassName == null && item.StudentId.HasValue && studentClassMap.TryGetValue(item.StudentId.Value, out var clsName))
            {
                item.CurrentClassName = clsName;
            }
            if (item.ClassTransfer != null && activeClassCounts.TryGetValue(item.ClassTransfer.ToClassId, out var count) && count > 0)
            {
                item.ClassTransfer.TargetClassCapacity = count;
            }
        }

        var orderMap = pageIds.Select((id, idx) => new { id, idx }).ToDictionary(x => x.id, x => x.idx);
        var ordered = rows.OrderBy(r => orderMap[r.Id]).ToList();
        NormalizeSummaryDates(ordered);
        return (ordered, totalCount);
    }

    private static IQueryable<AdministrativeRequestSummaryDto> ProjectToSummaryDto(
        IQueryable<AdministrativeRequest> query,
        bool includeDraftContent)
    {
        return query.Select(r => new AdministrativeRequestSummaryDto
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber,
            RequestType = r.RequestType,
            Status = r.Status,
            SubmissionDate = r.SubmissionDate,
            ProcessedDate = r.ProcessedDate,
            RequesterUserId = r.RequesterUserId,
            RequesterName = r.RequesterUser != null ? r.RequesterUser.FullName : "Phụ huynh",
            StudentId = r.StudentId,
            StudentCode = r.Student != null ? r.Student.StudentCode : null,
            StudentName = r.Student != null ? r.Student.FullName : null,
            CurrentClassName = r.ClassTransferDetail != null && r.ClassTransferDetail.FromClass != null
                ? r.ClassTransferDetail.FromClass.ClassName
                : (r.Student != null
                    ? r.Student.ClassAssignments
                        .Where(ca => ca.IsActive && ca.Class != null)
                        .Select(ca => ca.Class.ClassName)
                        .FirstOrDefault()
                    : null),
            RequiresAmendment = r.RequiresAmendment,
            AttachmentUrl = r.AttachmentUrl,
            AmendmentNotes = r.AmendmentNotes,
            ReviewerNotes = r.ReviewerNotes,
            SchoolBoardNotes = r.SchoolBoardNotes,
            SchoolBoardApprovedAt = r.SchoolBoardApprovedAt,
            ClassTransfer = r.ClassTransferDetail == null ? null : new ClassTransferSummaryDto
            {
                FromClassId = r.ClassTransferDetail.FromClassId,
                FromClassName = r.ClassTransferDetail.FromClass != null ? r.ClassTransferDetail.FromClass.ClassName : string.Empty,
                ToClassId = r.ClassTransferDetail.ToClassId,
                ToClassName = r.ClassTransferDetail.ToClass != null ? r.ClassTransferDetail.ToClass.ClassName : string.Empty,
                TransferReason = r.ClassTransferDetail.TransferReason,
                StaffRecommendation = r.ClassTransferDetail.StaffRecommendation,
                TargetClassCapacity = r.ClassTransferDetail.ToClass != null && r.ClassTransferDetail.ToClass.ClassAssignments.Any(ca => ca.IsActive)
                    ? r.ClassTransferDetail.ToClass.ClassAssignments.Count(ca => ca.IsActive)
                    : (r.ClassTransferDetail.TargetClassCapacitySnapshot ?? 0),
                TargetClassMaxCapacity = r.ClassTransferDetail.ToClass != null ? r.ClassTransferDetail.ToClass.Capacity : null,
                IsExecuted = r.ClassTransferDetail.IsExecuted,
                ExecutedAt = r.ClassTransferDetail.ExecutedAt,
                EffectiveDate = r.ClassTransferDetail.EffectiveDate
            },
            DocumentRequest = r.DocumentRequestDetail == null ? null : new DocumentRequestSummaryDto
            {
                DocumentType = r.DocumentRequestDetail.DocumentType,
                NumberOfCopies = r.DocumentRequestDetail.NumberOfCopies,
                Purpose = r.DocumentRequestDetail.Purpose,
                StaffEndorsement = r.DocumentRequestDetail.StaffEndorsement,
                DocumentContentDraft = includeDraftContent ? r.DocumentRequestDetail.DocumentContentDraft : null,
                IssuedDate = r.DocumentRequestDetail.IssuedDate,
                IssuedDocumentUrl = r.DocumentRequestDetail.IssuedDocumentUrl,
                CertificateNumber = r.DocumentRequestDetail.CertificateNumber,
                DigitalSignatureInfo = r.DocumentRequestDetail.DigitalSignatureInfo,
                IsPaperCopyRequested = r.DocumentRequestDetail.IsPaperCopyRequested
            }
        });
    }

    private static void NormalizeSummaryDates(IEnumerable<AdministrativeRequestSummaryDto> dtos)
    {
        foreach (var dto in dtos)
        {
            NormalizeSummaryDates(dto);
        }
    }

    private static void NormalizeSummaryDates(AdministrativeRequestSummaryDto dto)
    {
        dto.SubmissionDate = AsUtc(dto.SubmissionDate);
        dto.ProcessedDate = AsUtc(dto.ProcessedDate);
        dto.SchoolBoardApprovedAt = AsUtc(dto.SchoolBoardApprovedAt);
        if (dto.ClassTransfer != null)
        {
            dto.ClassTransfer.ExecutedAt = AsUtc(dto.ClassTransfer.ExecutedAt);
        }
        if (dto.DocumentRequest != null)
        {
            dto.DocumentRequest.IssuedDate = AsUtc(dto.DocumentRequest.IssuedDate);
        }
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
                .Where(sg => sg.Guardian.UserId != null)
                .Select(sg => sg.Guardian.UserId!.Value)
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
