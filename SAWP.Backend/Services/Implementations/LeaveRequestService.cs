using System.Text.Json;
using Core.Common;
using Core.Configuration;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.ExternalServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILunchService _lunchService;
    private readonly ISchoolClock _clock;
    private readonly SchoolOptions _options;
    private readonly IFileStorageService _fileStorage;
    private readonly IAuditService _auditService;
    private readonly ISchoolCalendarService _calendarService;

    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private const string AttachmentUrlPrefix = "/api/LeaveRequests/attachment-file?path=";
    private static readonly string[] AllowedAttachmentExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };

    public LeaveRequestService(
        IUnitOfWork unitOfWork,
        ILunchService lunchService,
        ISchoolClock clock,
        IOptions<SchoolOptions> schoolOptions,
        IFileStorageService fileStorage,
        IAuditService auditService,
        ISchoolCalendarService? calendarService = null)
    {
        _fileStorage = fileStorage;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
        _lunchService = lunchService;
        _clock = clock;
        _options = schoolOptions.Value;
        _calendarService = calendarService ?? new SchoolCalendarService(unitOfWork);
    }

    private DateTime CurrentToday => _clock.Today != default ? _clock.Today.Date : DateTime.Today;
    private DateTime CurrentNow => _clock.Now != default ? _clock.Now : DateTime.Now;
    private int EscalationThresholdDays => Math.Max(1, _options.LeaveEscalationThresholdDays);
    private int MaxLeaveSchoolDays => Math.Max(1, _options.MaxLeaveSchoolDays);
    private int MaxLeaveCalendarSpanDays => Math.Max(1, _options.MaxLeaveCalendarSpanDays);

    private async Task<Dictionary<DateTime, bool>> GetCalendarDayMapAsync(DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        var calRepo = _unitOfWork.Repository<SchoolCalendarDay>();
        if (calRepo == null) return new Dictionary<DateTime, bool>();

        var rows = await calRepo.Query()
            .Where(d => d.Date >= startDate.Date && d.Date <= endDate.Date)
            .ToListAsync(ct);

        return rows
            .GroupBy(d => d.Date.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().IsSchoolDay);
    }

    private static List<DateTime> ExtractSchoolDays(DateTime startDate, DateTime endDate, IReadOnlyDictionary<DateTime, bool> calendarMap)
    {
        var result = new List<DateTime>();
        for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
        {
            if (SchoolCalendarHelper.IsSchoolDay(d, calendarMap))
            {
                result.Add(d);
            }
        }
        return result;
    }

    private async Task<List<DateTime>> GetSchoolDaysInRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        if (endDate.Date < startDate.Date) return new List<DateTime>();
        var calendarMap = await GetCalendarDayMapAsync(startDate, endDate, ct);
        return ExtractSchoolDays(startDate, endDate, calendarMap);
    }

    private async Task<Dictionary<DateTime, LunchServiceDay>> GetServiceDayMapAsync(DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        var repo = _unitOfWork.Repository<LunchServiceDay>();
        if (repo == null) return new Dictionary<DateTime, LunchServiceDay>();

        var rows = await repo.Query()
            .Where(d => d.Date >= startDate.Date && d.Date <= endDate.Date)
            .ToListAsync(ct);

        return rows
            .GroupBy(d => d.Date.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());
    }

    private TimeSpan GetEffectiveCutoff(DateTime date, IReadOnlyDictionary<DateTime, LunchServiceDay> serviceDayMap)
    {
        if (serviceDayMap.TryGetValue(date.Date, out var sDay) && sDay.CutoffTime != TimeSpan.Zero)
        {
            return sDay.CutoffTime;
        }
        return _options.LunchCutoff;
    }

    private bool IsCutoffPassed(DateTime date, IReadOnlyDictionary<DateTime, LunchServiceDay> serviceDayMap)
    {
        if (serviceDayMap.TryGetValue(date.Date, out var sDay) && sDay.IsLocked)
        {
            return true;
        }
        var cutoff = GetEffectiveCutoff(date, serviceDayMap);
        return CurrentNow >= date.Date.Add(cutoff);
    }

    private bool IsWithdrawCutoffPassed(DateTime date, LeaveSession session, IReadOnlyDictionary<DateTime, LunchServiceDay> serviceDayMap)
    {
        if (session == LeaveSession.Afternoon)
        {
            return CurrentNow >= date.Date.Add(_options.AfternoonCutoff);
        }

        return IsCutoffPassed(date, serviceDayMap);
    }

    private static bool IsValidAttachmentUrls(string? rawUrls)
    {
        if (string.IsNullOrWhiteSpace(rawUrls)) return true;

        var parts = rawUrls.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return true;

        foreach (var part in parts)
        {
            if (!part.StartsWith(AttachmentUrlPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (part.Contains("..", StringComparison.Ordinal) ||
                part.Contains("://", StringComparison.Ordinal) ||
                part.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
                part.Any(char.IsWhiteSpace))
            {
                return false;
            }

            var encodedPath = part[AttachmentUrlPrefix.Length..];
            if (string.IsNullOrWhiteSpace(encodedPath))
            {
                return false;
            }

            var decodedPath = Uri.UnescapeDataString(encodedPath);
            if (decodedPath.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            var ext = Path.GetExtension(decodedPath).ToLowerInvariant();
            if (!AllowedAttachmentExtensions.Contains(ext))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<(bool IsValid, string? ErrorMessage, SystemMessage? ErrorCode, List<DateTime> SchoolDays)> ValidateLeaveInputAsync(
        int studentId,
        DateTime startDate,
        DateTime endDate,
        LeaveSession session,
        string reason,
        string? attachmentUrl,
        int? excludeRequestId,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(typeof(LeaveSession), session))
        {
            return (false, "Buổi xin nghỉ không hợp lệ.", SystemMessages.MSG01, new List<DateTime>());
        }

        var start = startDate.Date;
        var end = endDate.Date;

        if (start < CurrentToday)
        {
            return (false, "Không thể xin nghỉ học cho ngày trong quá khứ.", SystemMessages.MSG24, new List<DateTime>());
        }

        if (start > end)
        {
            return (false, "Ngày bắt đầu nghỉ không được lớn hơn ngày kết thúc.", SystemMessages.MSG24, new List<DateTime>());
        }

        var calendarSpanDays = (end - start).Days + 1;
        if (calendarSpanDays > MaxLeaveCalendarSpanDays)
        {
            return (false, $"Khoảng thời gian xin nghỉ không được vượt quá {MaxLeaveCalendarSpanDays} ngày lịch.", SystemMessages.MSG01, new List<DateTime>());
        }

        var schoolDays = await GetSchoolDaysInRangeAsync(start, end, ct);
        if (schoolDays.Count == 0)
        {
            return (false, "Khoảng thời gian xin nghỉ chỉ gồm ngày cuối tuần hoặc ngày nghỉ lễ. Vui lòng chọn ngày học.", SystemMessages.MSG24, new List<DateTime>());
        }

        if (schoolDays.Count > MaxLeaveSchoolDays)
        {
            return (false, $"Tổng số ngày học xin nghỉ không được vượt quá {MaxLeaveSchoolDays} ngày học.", SystemMessages.MSG01, new List<DateTime>());
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return (false, "Vui lòng nhập lý do xin nghỉ học.", SystemMessages.MSG01, new List<DateTime>());
        }

        if (!IsValidAttachmentUrls(attachmentUrl))
        {
            return (false, "Đường dẫn tệp đính kèm không hợp lệ. Vui lòng tải tệp lên qua hệ thống (chỉ hỗ trợ PDF, JPG, PNG).", SystemMessages.MSG41, new List<DateTime>());
        }

        var duplicate = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.LeaveRequest)
            .Where(ar => ar.StudentId == studentId && ar.RequestType == RequestTypes.Leave)
            .Where(ar => !excludeRequestId.HasValue || ar.Id != excludeRequestId.Value)
            .Where(ar => ar.Status == RequestStatus.Submitted
                      || ar.Status == RequestStatus.Approved
                      || ar.Status == RequestStatus.UnderReview
                      || ar.Status == RequestStatus.NeedMoreInfo)
            .AnyAsync(ar => ar.LeaveRequest != null &&
                            ar.LeaveRequest.StartDate.Date <= end &&
                            ar.LeaveRequest.EndDate.Date >= start &&
                            (ar.LeaveRequest.Session == LeaveSession.FullDay ||
                             session == LeaveSession.FullDay ||
                             ar.LeaveRequest.Session == session), ct);

        if (duplicate)
        {
            return (false, "Học sinh đã có đơn xin nghỉ trùng khoảng thời gian này đang chờ duyệt hoặc đã duyệt.", SystemMessages.MSG24, new List<DateTime>());
        }

        return (true, null, null, schoolDays);
    }

    private async Task NotifyHomeroomTeacherAsync(int studentId, string title, string content, string linkUrl, CancellationToken ct)
    {
        var classRepo = _unitOfWork.Repository<ClassAssignment>();
        var notifRepo = _unitOfWork.Repository<Notification>();
        if (classRepo == null || notifRepo == null) return;

        var currentAssignment = await classRepo.Query()
            .Include(ca => ca.Class)
                .ThenInclude(c => c.HomeroomTeacher)
            .FirstOrDefaultAsync(ca => ca.StudentId == studentId && ca.IsActive, ct);

        if (currentAssignment?.Class?.HomeroomTeacher?.UserId != null)
        {
            await notifRepo.AddAsync(new Notification
            {
                UserId = currentAssignment.Class.HomeroomTeacher.UserId,
                Title = title,
                Content = content,
                LinkUrl = linkUrl,
                CreatedAt = DateTime.UtcNow
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<ApiResponse<LeaveRequestItemDto>> SubmitLeaveRequestAsync(int parentUserId, CreateLeaveRequestDto request, CancellationToken cancellationToken = default)
    {
        var guardian = await _unitOfWork.Repository<Guardian>()
            .Query()
            .Include(g => g.StudentGuardians)
            .FirstOrDefaultAsync(g => g.UserId == parentUserId, cancellationToken);

        if (guardian == null)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Không tìm thấy thông tin phụ huynh / người giám hộ.");
        }

        var isLinked = guardian.StudentGuardians.Any(sg => sg.StudentId == request.StudentId);
        if (!isLinked)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Bạn không có quyền gửi đơn cho học sinh này.");
        }

        var validation = await ValidateLeaveInputAsync(
            request.StudentId,
            request.StartDate,
            request.EndDate,
            request.Session,
            request.Reason,
            request.DoctorNoteAttachmentUrl,
            null,
            cancellationToken);

        if (!validation.IsValid)
        {
            return validation.ErrorCode != null
                ? ApiResponse<LeaveRequestItemDto>.Fail(validation.ErrorCode, new List<string> { validation.ErrorMessage! })
                : ApiResponse<LeaveRequestItemDto>.Fail(validation.ErrorMessage!);
        }

        var requestNumber = $"LR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var adminRequest = new AdministrativeRequest
        {
            RequestNumber = requestNumber,
            RequesterUserId = parentUserId,
            StudentId = request.StudentId,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            SubmissionDate = DateTime.UtcNow
        };

        await _unitOfWork.Repository<AdministrativeRequest>().AddAsync(adminRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var leaveDetail = new LeaveRequest
        {
            AdministrativeRequestId = adminRequest.Id,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            Session = request.Session,
            KeepLunchService = request.KeepLunchService,
            Reason = request.Reason.Trim(),
            DoctorNoteAttachmentUrl = string.IsNullOrWhiteSpace(request.DoctorNoteAttachmentUrl) ? null : request.DoctorNoteAttachmentUrl.Trim()
        };

        await _unitOfWork.Repository<LeaveRequest>().AddAsync(leaveDetail, cancellationToken);
        await _auditService.LogActionAsync(
            parentUserId,
            AuditActions.LeaveRequestSubmit,
            nameof(AdministrativeRequest),
            adminRequest.Id.ToString(),
            null,
            new
            {
                RequestNumber = requestNumber,
                request.StudentId,
                StartDate = request.StartDate.Date,
                EndDate = request.EndDate.Date,
                SchoolDaysCount = validation.SchoolDays.Count,
                request.Session,
                request.KeepLunchService
            },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyHomeroomTeacherAsync(
            request.StudentId,
            "Đơn xin nghỉ học mới",
            $"Phụ huynh học sinh đã gửi đơn xin nghỉ từ {request.StartDate:dd-MM-yyyy} đến {request.EndDate:dd-MM-yyyy} ({validation.SchoolDays.Count} ngày học). Mã đơn: {requestNumber}",
            $"/teacher/leave-requests?requestId={adminRequest.Id}",
            cancellationToken);

        var resultDto = await MapToDtoAsync(adminRequest.Id, cancellationToken);
        return ApiResponse<LeaveRequestItemDto>.Ok(resultDto!, SystemMessages.MSG21);
    }

    public async Task<ApiResponse<LeaveRequestItemDto>> UpdateLeaveRequestAsync(
        int requestId,
        int parentUserId,
        UpdateLeaveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.LeaveRequest)
            .Include(ar => ar.Student)
            .FirstOrDefaultAsync(ar => ar.Id == requestId && ar.RequestType == RequestTypes.Leave && ar.RequesterUserId == parentUserId, cancellationToken);

        if (req == null || req.LeaveRequest == null)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Không tìm thấy đơn xin nghỉ học.");
        }

        if (req.Status != RequestStatus.Submitted && req.Status != RequestStatus.NeedMoreInfo)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail(
                SystemMessages.MSG09,
                new List<string> { "Chỉ có thể chỉnh sửa đơn khi đang ở trạng thái Chờ duyệt hoặc Yêu cầu bổ sung thông tin." });
        }

        var studentId = req.StudentId ?? 0;
        var validation = await ValidateLeaveInputAsync(
            studentId,
            request.StartDate,
            request.EndDate,
            request.Session,
            request.Reason,
            request.DoctorNoteAttachmentUrl,
            req.Id,
            cancellationToken);

        if (!validation.IsValid)
        {
            return validation.ErrorCode != null
                ? ApiResponse<LeaveRequestItemDto>.Fail(validation.ErrorCode, new List<string> { validation.ErrorMessage! })
                : ApiResponse<LeaveRequestItemDto>.Fail(validation.ErrorMessage!);
        }

        var oldValues = new
        {
            req.Status,
            req.LeaveRequest.StartDate,
            req.LeaveRequest.EndDate,
            req.LeaveRequest.Session,
            req.LeaveRequest.KeepLunchService,
            req.LeaveRequest.Reason
        };

        req.LeaveRequest.StartDate = request.StartDate.Date;
        req.LeaveRequest.EndDate = request.EndDate.Date;
        req.LeaveRequest.Session = request.Session;
        req.LeaveRequest.KeepLunchService = request.KeepLunchService;
        req.LeaveRequest.Reason = request.Reason.Trim();
        req.LeaveRequest.DoctorNoteAttachmentUrl = string.IsNullOrWhiteSpace(request.DoctorNoteAttachmentUrl) ? null : request.DoctorNoteAttachmentUrl.Trim();
        req.Status = RequestStatus.Submitted;
        req.RequiresAmendment = false;

        await _auditService.LogActionAsync(
            parentUserId,
            AuditActions.LeaveRequestResubmit,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            oldValues,
            new
            {
                Status = RequestStatus.Submitted,
                StartDate = req.LeaveRequest.StartDate,
                EndDate = req.LeaveRequest.EndDate,
                SchoolDaysCount = validation.SchoolDays.Count,
                req.LeaveRequest.Session,
                req.LeaveRequest.KeepLunchService,
                Notes = "Phụ huynh cập nhật và gửi lại đơn"
            },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (req.StudentId.HasValue)
        {
            await NotifyHomeroomTeacherAsync(
                req.StudentId.Value,
                "Phụ huynh đã cập nhật đơn xin nghỉ học",
                $"Đơn xin nghỉ {req.RequestNumber} của học sinh {req.Student?.FullName} đã được phụ huynh bổ sung/chỉnh sửa ({request.StartDate:dd-MM-yyyy} - {request.EndDate:dd-MM-yyyy}, {validation.SchoolDays.Count} ngày học).",
                $"/teacher/leave-requests?requestId={req.Id}",
                cancellationToken);
        }

        var resultDto = await MapToDtoAsync(req.Id, cancellationToken);
        return ApiResponse<LeaveRequestItemDto>.Ok(resultDto!, "Đã cập nhật và gửi lại đơn xin nghỉ học thành công.");
    }

    public async Task<ApiResponse<List<LeaveRequestItemDto>>> GetMyLeaveRequestsAsync(int parentUserId, CancellationToken cancellationToken = default)
    {
        var requests = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.Student)
            .Include(ar => ar.RequesterUser)
            .Include(ar => ar.ReviewerUser)
            .Include(ar => ar.LeaveRequest)
            .Where(ar => ar.RequesterUserId == parentUserId && ar.RequestType == RequestTypes.Leave)
            .OrderByDescending(ar => ar.SubmissionDate)
            .ToListAsync(cancellationToken);

        var list = await MapToDtosAsync(requests, cancellationToken);
        return ApiResponse<List<LeaveRequestItemDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<LeaveRequestItemDto>>> GetClassLeaveRequestsAsync(int teacherUserId, int? classId = null, string? status = null, DateTime? date = null, CancellationToken cancellationToken = default)
    {
        var userRoles = await _unitOfWork.Repository<UserRole>()
            .Query()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == teacherUserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken);

        var isSchoolBoardOrAdmin = userRoles.Any(r => r == "SchoolBoard" || r == "Admin");

        var queryClasses = _unitOfWork.Repository<Class>().Query();
        if (!isSchoolBoardOrAdmin)
        {
            var staffRepo = _unitOfWork.Repository<StaffProfile>();
            var teacherProfile = staffRepo != null
                ? await staffRepo.Query().FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken)
                : null;

            if (teacherProfile != null)
            {
                queryClasses = queryClasses.Where(c => c.HomeroomTeacherId == teacherProfile.Id);
            }
            else if (!classId.HasValue)
            {
                return ApiResponse<List<LeaveRequestItemDto>>.Ok(new List<LeaveRequestItemDto>());
            }
        }
        if (classId.HasValue)
        {
            queryClasses = queryClasses.Where(c => c.Id == classId.Value);
        }

        var targetClassIds = await queryClasses.Select(c => c.Id).ToListAsync(cancellationToken);

        var activeStudentIds = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .Where(ca => targetClassIds.Contains(ca.ClassId) && ca.IsActive)
            .Select(ca => ca.StudentId)
            .ToListAsync(cancellationToken);

        var reqQuery = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.Student)
            .Include(ar => ar.RequesterUser)
            .Include(ar => ar.ReviewerUser)
            .Include(ar => ar.LeaveRequest)
            .Where(ar => ar.RequestType == RequestTypes.Leave && ar.StudentId.HasValue && activeStudentIds.Contains(ar.StudentId.Value));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, true, out var parsedStatus))
        {
            reqQuery = reqQuery.Where(ar => ar.Status == parsedStatus);
        }

        if (date.HasValue)
        {
            var d = date.Value.Date;
            reqQuery = reqQuery.Where(ar => ar.LeaveRequest != null && ar.LeaveRequest.StartDate.Date <= d && ar.LeaveRequest.EndDate.Date >= d);
        }

        var requests = await reqQuery.OrderByDescending(ar => ar.SubmissionDate).ToListAsync(cancellationToken);
        var list = await MapToDtosAsync(requests, cancellationToken);
        return ApiResponse<List<LeaveRequestItemDto>>.Ok(list);
    }

    private async Task<bool> CanAccessLeaveRequestAsync(
        AdministrativeRequest req,
        int requestingUserId,
        IReadOnlyList<UserRoles>? requestingRoles,
        CancellationToken cancellationToken)
    {
        if (requestingUserId <= 0) return false;
        if (req.RequesterUserId == requestingUserId) return true;

        var roles = requestingRoles;
        if (roles == null)
        {
            var userRoleRepo = _unitOfWork.Repository<UserRole>();
            if (userRoleRepo != null)
            {
                var roleNames = await userRoleRepo.Query()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == requestingUserId)
                    .Select(ur => ur.Role.Name)
                    .ToListAsync(cancellationToken);

                var parsed = new List<UserRoles>();
                foreach (var rn in roleNames)
                {
                    if (Enum.TryParse<UserRoles>(rn, true, out var r)) parsed.Add(r);
                }
                roles = parsed;
            }
            else
            {
                roles = Array.Empty<UserRoles>();
            }
        }

        if (roles.Contains(UserRoles.SchoolBoard) || roles.Contains(UserRoles.Admin))
        {
            return true;
        }

        if (roles.Contains(UserRoles.Teacher) && req.StudentId.HasValue)
        {
            var staffRepo = _unitOfWork.Repository<StaffProfile>();
            var caRepo = _unitOfWork.Repository<ClassAssignment>();
            if (staffRepo != null && caRepo != null)
            {
                var staffProfile = await staffRepo.Query()
                    .FirstOrDefaultAsync(sp => sp.UserId == requestingUserId, cancellationToken);
                if (staffProfile != null)
                {
                    var currentClass = await caRepo.Query()
                        .Include(ca => ca.Class)
                        .Where(ca => ca.StudentId == req.StudentId.Value && ca.IsActive)
                        .OrderByDescending(ca => ca.StartDate)
                        .ThenByDescending(ca => ca.Id)
                        .Select(ca => ca.Class)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (currentClass != null && currentClass.HomeroomTeacherId == staffProfile.Id)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public async Task<ApiResponse<LeaveRequestItemDto>> GetLeaveRequestDetailsAsync(
        int requestId,
        int requestingUserId,
        IReadOnlyList<UserRoles>? requestingRoles = null,
        CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.Student)
            .Include(ar => ar.RequesterUser)
            .Include(ar => ar.ReviewerUser)
            .Include(ar => ar.LeaveRequest)
            .FirstOrDefaultAsync(ar => ar.Id == requestId && ar.RequestType == RequestTypes.Leave, cancellationToken);

        if (req == null || req.LeaveRequest == null)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Không tìm thấy đơn xin nghỉ học.");
        }

        var canAccess = await CanAccessLeaveRequestAsync(req, requestingUserId, requestingRoles, cancellationToken);
        if (!canAccess)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail(
                SystemMessages.MSG10,
                new List<string> { "Bạn không có quyền xem chi tiết đơn xin nghỉ học này." });
        }

        var list = await MapToDtosAsync(new List<AdministrativeRequest> { req }, cancellationToken);
        var dto = list.FirstOrDefault();
        if (dto == null)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Không tìm thấy đơn xin nghỉ học.");
        }

        return ApiResponse<LeaveRequestItemDto>.Ok(dto);
    }

    public async Task<ApiResponse<SyncLeaveResultDto>> ReviewLeaveRequestAsync(int requestId, int reviewerUserId, ReviewLeaveRequestDto review, IReadOnlyList<UserRoles>? reviewerRoles = null, CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.LeaveRequest)
            .Include(ar => ar.Student)
            .FirstOrDefaultAsync(ar => ar.Id == requestId && ar.RequestType == RequestTypes.Leave, cancellationToken);

        if (req == null || req.LeaveRequest == null)
        {
            return ApiResponse<SyncLeaveResultDto>.Fail("Không tìm thấy đơn xin nghỉ học.");
        }

        if (reviewerRoles != null)
        {
            var isBoardOrAdmin = reviewerRoles.Contains(UserRoles.SchoolBoard) || reviewerRoles.Contains(UserRoles.Admin);
            var isTeacher = reviewerRoles.Contains(UserRoles.Teacher);
            if (!isBoardOrAdmin && !isTeacher)
            {
                return ApiResponse<SyncLeaveResultDto>.Fail(SystemMessages.MSG10, new List<string> { "Bạn không có quyền phê duyệt hoặc từ chối đơn xin nghỉ học." });
            }

            if (!isBoardOrAdmin && isTeacher && req.StudentId.HasValue)
            {
                var staffProfile = await _unitOfWork.Repository<StaffProfile>()
                    .Query()
                    .FirstOrDefaultAsync(sp => sp.UserId == reviewerUserId, cancellationToken);

                var studentClasses = await _unitOfWork.Repository<ClassAssignment>()
                    .Query()
                    .Include(ca => ca.Class)
                    .Where(ca => ca.StudentId == req.StudentId.Value && ca.IsActive)
                    .OrderByDescending(ca => ca.StartDate)
                    .ThenByDescending(ca => ca.Id)
                    .ToListAsync(cancellationToken);

                var currentClass = studentClasses.FirstOrDefault()?.Class;
                if (staffProfile == null || currentClass == null || currentClass.HomeroomTeacherId != staffProfile.Id)
                {
                    return ApiResponse<SyncLeaveResultDto>.Fail(SystemMessages.MSG10, new List<string> { "Bạn không phải giáo viên chủ nhiệm của lớp học sinh này." });
                }
            }
        }

        if (req.Status != RequestStatus.Submitted && req.Status != RequestStatus.UnderReview)
        {
            return ApiResponse<SyncLeaveResultDto>.Fail($"Đơn đã được xử lý trước đó với trạng thái: {req.Status}.");
        }

        var threshold = EscalationThresholdDays;
        var schoolDays = await GetSchoolDaysInRangeAsync(req.LeaveRequest.StartDate, req.LeaveRequest.EndDate, cancellationToken);
        var durationSchoolDays = schoolDays.Count;
        var syncResult = new SyncLeaveResultDto { RequestId = req.Id };

        // NHÁNH 1: Yêu cầu phụ huynh bổ sung thông tin (A10)
        if (review.IsRequestMoreInfo)
        {
            if (string.IsNullOrWhiteSpace(review.ReviewerNotes))
            {
                return ApiResponse<SyncLeaveResultDto>.Fail("Khi yêu cầu bổ sung thông tin, bắt buộc phải nhập nội dung cần bổ sung.");
            }

            var isBoardLevel = req.Status == RequestStatus.UnderReview || durationSchoolDays > threshold;
            var cleanNotes = review.ReviewerNotes.Trim();
            req.Status = RequestStatus.NeedMoreInfo;
            req.RequiresAmendment = true;
            req.AmendmentNotes = cleanNotes;
            req.ReviewerUserId = reviewerUserId;
            req.ProcessedDate = DateTime.UtcNow;
            req.ReviewerNotes = isBoardLevel
                ? $"[BGH yêu cầu bổ sung]: {cleanNotes}"
                : $"[GVCN yêu cầu bổ sung]: {cleanNotes}";

            await _auditService.LogActionAsync(
                reviewerUserId,
                AuditActions.LeaveRequestRequestMoreInfo,
                nameof(AdministrativeRequest),
                req.Id.ToString(),
                null,
                new { req.Status, ReviewLevel = isBoardLevel ? "SchoolBoard" : "Teacher", Notes = cleanNotes },
                null,
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                UserId = req.RequesterUserId,
                Title = "Đơn xin nghỉ học cần bổ sung thông tin",
                Content = $"Đơn xin nghỉ {req.RequestNumber} của học sinh {req.Student?.FullName} cần bổ sung thông tin: {cleanNotes}",
                LinkUrl = $"/parent/leave-requests?requestId={req.Id}",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            syncResult.Status = RequestStatus.NeedMoreInfo;
            syncResult.MealSyncMessage = "Đã trả đơn yêu cầu phụ huynh bổ sung thông tin.";
            return ApiResponse<SyncLeaveResultDto>.Ok(syncResult, "Đã gửi yêu cầu bổ sung thông tin tới phụ huynh.");
        }

        // NHÁNH 2: Giáo viên xác nhận yêu cầu và chuyển lên Ban Giám hiệu (khi nghỉ > ngưỡng ngày học, mặc định > 3)
        if (review.IsForwardToSchoolBoard)
        {
            if (durationSchoolDays <= threshold)
            {
                return ApiResponse<SyncLeaveResultDto>.Fail($"Chỉ đơn xin nghỉ trên {threshold} ngày học mới cần chuyển Ban Giám Hiệu phê duyệt.");
            }

            if (req.Status != RequestStatus.Submitted)
            {
                return ApiResponse<SyncLeaveResultDto>.Fail("Đơn đã được chuyển tiếp hoặc đã xử lý.");
            }

            var forwardNotes = string.IsNullOrWhiteSpace(review.ReviewerNotes)
                ? "Đã xác nhận thông tin, kính chuyển Ban Giám Hiệu xem xét phê duyệt."
                : review.ReviewerNotes.Trim();

            req.Status = RequestStatus.UnderReview;
            req.ReviewerUserId = reviewerUserId;
            req.ProcessedDate = DateTime.UtcNow;
            req.ReviewerNotes = $"[Ý kiến GVCN]: {forwardNotes}";

            await _auditService.LogActionAsync(
                reviewerUserId,
                AuditActions.LeaveRequestForward,
                nameof(AdministrativeRequest),
                req.Id.ToString(),
                new { Status = RequestStatus.Submitted },
                new { req.Status, SchoolDaysCount = durationSchoolDays, Notes = forwardNotes },
                null,
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var boardRole = await _unitOfWork.Repository<Role>().Query().FirstOrDefaultAsync(r => r.Name == "SchoolBoard", cancellationToken);
            if (boardRole != null)
            {
                var boardUserIds = await _unitOfWork.Repository<UserRole>().Query()
                    .Where(ur => ur.RoleId == boardRole.Id)
                    .Select(ur => ur.UserId)
                    .ToListAsync(cancellationToken);

                foreach (var bId in boardUserIds)
                {
                    await _unitOfWork.Repository<Notification>().AddAsync(new Notification
                    {
                        UserId = bId,
                        Title = $"Đơn nghỉ dài ngày cần duyệt (> {threshold} ngày học)",
                        Content = $"GVCN đã chuyển đơn nghỉ {durationSchoolDays} ngày học của học sinh {req.Student?.FullName}. Mã đơn: {req.RequestNumber}",
                        LinkUrl = "/teacher/leave-requests",
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
            }

            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                UserId = req.RequesterUserId,
                Title = "Đơn xin nghỉ học đã chuyển Ban Giám Hiệu",
                Content = $"Đơn xin nghỉ {durationSchoolDays} ngày học của con bạn ({req.Student?.FullName}) đã được GVCN xác nhận và chuyển tiếp lên Ban Giám Hiệu xem xét phê duyệt.",
                LinkUrl = $"/parent/leave-requests?requestId={req.Id}",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            syncResult.Status = RequestStatus.UnderReview;
            syncResult.MealSyncMessage = "Đã chuyển tiếp đơn nghỉ lên Ban Giám Hiệu thành công.";
            return ApiResponse<SyncLeaveResultDto>.Ok(syncResult, "Đã chuyển đơn lên Ban Giám Hiệu phê duyệt.");
        }

        // NHÁNH 3: QUYẾT ĐỊNH Phê duyệt hoặc Từ chối (GVCN <= ngưỡng ngày học; BGH > ngưỡng ngày học)
        var isEscalatedDecision = req.Status == RequestStatus.UnderReview || durationSchoolDays > threshold;
        if (isEscalatedDecision && reviewerRoles != null && !reviewerRoles.Contains(UserRoles.SchoolBoard) && !reviewerRoles.Contains(UserRoles.Admin))
        {
            return ApiResponse<SyncLeaveResultDto>.Fail($"Chỉ Ban Giám Hiệu mới có quyền phê duyệt hoặc từ chối đơn xin nghỉ trên {threshold} ngày học.");
        }

        req.ReviewerUserId = reviewerUserId;
        req.ProcessedDate = DateTime.UtcNow;

        // 3A. Từ chối
        if (!review.IsApproved)
        {
            if (string.IsNullOrWhiteSpace(review.ReviewerNotes))
            {
                return ApiResponse<SyncLeaveResultDto>.Fail("Khi từ chối đơn xin nghỉ, bắt buộc phải nhập lý do.");
            }

            var cleanRejectNote = review.ReviewerNotes.Trim();
            req.ReviewerNotes = isEscalatedDecision
                ? $"[BGH từ chối]: {cleanRejectNote}"
                : $"[GVCN từ chối]: {cleanRejectNote}";
            req.Status = RequestStatus.Rejected;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditService.LogActionAsync(
                reviewerUserId,
                AuditActions.LeaveRequestReject,
                nameof(AdministrativeRequest),
                req.Id.ToString(),
                null,
                new { req.Status, ReviewLevel = isEscalatedDecision ? "SchoolBoard" : "Teacher", Notes = cleanRejectNote },
                null,
                cancellationToken);

            var rejectTitle = isEscalatedDecision
                ? "Thông báo từ chối đơn chuyển cấp"
                : "Thông báo từ chối đơn thông thường";

            var rejectContent = isEscalatedDecision
                ? $"{SystemMessages.MSG23.Content} Đơn xin nghỉ {durationSchoolDays} ngày học của con bạn ({req.Student?.FullName}) đã bị Ban Giám Hiệu từ chối. Lý do: {cleanRejectNote}"
                : $"{SystemMessages.MSG23.Content} Đơn xin nghỉ học của con bạn ({req.Student?.FullName}) đã bị GVCN từ chối. Lý do: {cleanRejectNote}";

            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                UserId = req.RequesterUserId,
                Title = rejectTitle,
                Content = rejectContent,
                LinkUrl = $"/parent/leave-requests?requestId={req.Id}",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            syncResult.Status = RequestStatus.Rejected;
            syncResult.MealSyncMessage = "Đơn bị từ chối. Trạng thái điểm danh và suất ăn giữ nguyên.";
            return ApiResponse<SyncLeaveResultDto>.Ok(syncResult, SystemMessages.MSG23);
        }

        // 3B. Phê duyệt
        var cleanApproveNote = string.IsNullOrWhiteSpace(review.ReviewerNotes)
            ? "Đồng ý cho học sinh nghỉ học."
            : review.ReviewerNotes.Trim();

        req.ReviewerNotes = isEscalatedDecision
            ? $"[BGH phê duyệt]: {cleanApproveNote}"
            : $"[GVCN phê duyệt]: {cleanApproveNote}";
        req.Status = RequestStatus.Approved;
        syncResult.Status = RequestStatus.Approved;
        syncResult.IsAttendanceSyncSuggested = true;

        var skippedMealDates = new HashSet<DateTime>();
        var attendanceWarnings = new List<string>();
        var teacherConflictAlerts = new List<(int TargetUserId, int? ClassId, DateTime Date, AttendanceSession Session, AttendanceStatus ExistingStatus)>();

        // BR-26: Duyệt đơn chỉ cung cấp chỉ báo nghỉ trên bảng điểm danh (không tự ghi/ghi đè AttendanceRecord).
        // Nếu giáo viên đã chấm Có mặt / Đi muộn cho ngày trong đơn, gửi thông báo nhắc GVCN kiểm tra lại.
        var schoolDaysInLeave = await _calendarService.GetSchoolDaysInRangeAsync(
            req.LeaveRequest.StartDate.Date,
            req.LeaveRequest.EndDate.Date,
            cancellationToken);

        if (req.StudentId.HasValue && schoolDaysInLeave.Count > 0)
        {
            var attRepo = _unitOfWork.Repository<AttendanceRecord>();
            if (attRepo != null)
            {
                var startD = req.LeaveRequest.StartDate.Date;
                var endD = req.LeaveRequest.EndDate.Date;
                var existingPresentOrLate = await attRepo.Query()
                    .Where(ar => ar.StudentId == req.StudentId.Value &&
                                 ar.Date >= startD && ar.Date <= endD &&
                                 (ar.Status == AttendanceStatus.Present || ar.Status == AttendanceStatus.Late))
                    .Select(ar => new { ar.Date, ar.Session, ar.Status, ar.RecordedByUserId })
                    .ToListAsync(cancellationToken);

                var conflicts = existingPresentOrLate.Where(x => schoolDaysInLeave.Contains(x.Date.Date)).ToList();
                if (conflicts.Count > 0)
                {
                    var classAssignment = await _unitOfWork.Repository<ClassAssignment>()
                        .Query()
                        .Include(ca => ca.Class).ThenInclude(c => c.HomeroomTeacher)
                        .FirstOrDefaultAsync(ca => ca.StudentId == req.StudentId.Value && ca.IsActive, cancellationToken);
                    var homeroomUserId = classAssignment?.Class?.HomeroomTeacher?.UserId;

                    foreach (var c in conflicts)
                    {
                        var statusText = c.Status == AttendanceStatus.Present ? "Có mặt" : "Đi muộn";
                        var sesText = c.Session == AttendanceSession.Morning ? "Buổi sáng" : "Buổi chiều";
                        attendanceWarnings.Add($"Ngày {c.Date:dd-MM-yyyy} ({sesText}) học sinh đã được chấm {statusText}, hệ thống giữ nguyên và đã gửi cảnh báo tới giáo viên.");
                        if (c.Session == AttendanceSession.Morning) skippedMealDates.Add(c.Date.Date);
                        if (homeroomUserId.HasValue && homeroomUserId.Value > 0)
                        {
                            teacherConflictAlerts.Add((homeroomUserId.Value, classAssignment?.ClassId, c.Date.Date, c.Session, c.Status));
                        }
                        if (c.RecordedByUserId > 0 && c.RecordedByUserId != homeroomUserId)
                        {
                            teacherConflictAlerts.Add((c.RecordedByUserId, classAssignment?.ClassId, c.Date.Date, c.Session, c.Status));
                        }
                    }
                }
            }
        }

        // Gửi thông báo cho giáo viên nếu có xung đột điểm danh Present/Late
        var notifRepo = _unitOfWork.Repository<Notification>();
        if (notifRepo != null)
        {
            foreach (var alert in teacherConflictAlerts.Distinct())
            {
                var statusLabel = alert.ExistingStatus == AttendanceStatus.Present ? "Có mặt" : "Đi muộn";
                var sesLabel = alert.Session == AttendanceSession.Morning ? "Sáng" : "Chiều";
                await notifRepo.AddAsync(new Notification
                {
                    UserId = alert.TargetUserId,
                    Title = "Kiểm tra điểm danh trùng đơn xin nghỉ",
                    Content = $"Học sinh {req.Student?.FullName} có đơn nghỉ {req.RequestNumber} vừa được duyệt, nhưng ngày {alert.Date:dd-MM-yyyy} ({sesLabel}) bạn đã chấm \"{statusLabel}\". Vui lòng kiểm tra lại.",
                    LinkUrl = alert.ClassId.HasValue
                        ? $"/teacher/attendance?classId={alert.ClassId.Value}&date={alert.Date:yyyy-MM-dd}"
                        : "/teacher/attendance",
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        // 4. Handshake với Bán trú: chỉ hủy các NGÀY HỌC thực tế và không bị đánh dấu đã đi học buổi sáng
        var cancelledDates = new List<DateTime>();
        var shouldCheckLunch = !req.LeaveRequest.KeepLunchService &&
                               (req.LeaveRequest.Session == LeaveSession.FullDay || req.LeaveRequest.Session == LeaveSession.Morning);

        if (shouldCheckLunch && req.StudentId.HasValue)
        {
            foreach (var curDate in schoolDays)
            {
                if (skippedMealDates.Contains(curDate) || !schoolDaysInLeave.Contains(curDate))
                {
                    continue;
                }

                var cancelRes = await _lunchService.CancelMealForLeaveAsync(
                    req.StudentId.Value,
                    curDate,
                    $"Đơn nghỉ {req.RequestNumber}",
                    cancellationToken);

                if (cancelRes.Data)
                {
                    cancelledDates.Add(curDate);
                }
            }
        }

        req.LeaveRequest.IsLunchCancelled = cancelledDates.Any();

        await _auditService.LogActionAsync(
            reviewerUserId,
            AuditActions.LeaveRequestApprove,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            null,
            new { req.Status, ReviewLevel = isEscalatedDecision ? "SchoolBoard" : "Teacher", Notes = cleanApproveNote, SchoolDaysCount = durationSchoolDays },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var serviceDayMap = await GetServiceDayMapAsync(req.LeaveRequest.StartDate, req.LeaveRequest.EndDate, cancellationToken);
        var referenceMealDate = (cancelledDates.FirstOrDefault() != default ? cancelledDates.First() : (schoolDays.FirstOrDefault() != default ? schoolDays.First() : req.LeaveRequest.StartDate.Date));
        var effectiveCutoffLabel = GetEffectiveCutoff(referenceMealDate, serviceDayMap).ToString(@"hh\:mm");

        var mealNote = cancelledDates.Any()
            ? $"Suất ăn bán trú ngày ({string.Join(", ", cancelledDates.Select(d => d.ToString("dd-MM-yyyy")))}) đã được tự động ghi nhận hủy trước giờ chốt ({effectiveCutoffLabel})."
            : (req.LeaveRequest.KeepLunchService
                ? "Suất ăn bán trú được giữ nguyên theo yêu cầu trên đơn."
                : (skippedMealDates.Any()
                    ? "Học sinh đã được giáo viên điểm danh đi học nên suất ăn không bị tự động hủy."
                    : $"Suất ăn không bị hủy (sau giờ chốt {effectiveCutoffLabel} hoặc học sinh không đăng ký bán trú)."));

        var approveTitle = isEscalatedDecision
            ? "Thông báo phê duyệt đơn chuyển cấp"
            : "Thông báo phê duyệt đơn thông thường";

        var approveContent = isEscalatedDecision
            ? $"{SystemMessages.MSG22.Content} Ban Giám Hiệu đã phê duyệt đơn xin nghỉ {durationSchoolDays} ngày học của con bạn ({req.Student?.FullName}). {mealNote}"
            : $"{SystemMessages.MSG22.Content} Giáo viên đã phê duyệt đơn xin nghỉ học của con bạn ({req.Student?.FullName}). {mealNote}";

        await _unitOfWork.Repository<Notification>().AddAsync(new Notification
        {
            UserId = req.RequesterUserId,
            Title = approveTitle,
            Content = approveContent,
            LinkUrl = $"/parent/leave-requests?requestId={req.Id}",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        syncResult.CancelledMealDates = cancelledDates;
        syncResult.MealSyncMessage = mealNote;
        syncResult.AttendanceWarnings = attendanceWarnings;

        return ApiResponse<SyncLeaveResultDto>.Ok(syncResult, SystemMessages.MSG22);
    }

    public async Task<ApiResponse<bool>> CancelLeaveRequestAsync(int requestId, int parentUserId, CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .FirstOrDefaultAsync(ar => ar.Id == requestId && ar.RequesterUserId == parentUserId, cancellationToken);

        if (req == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn xin nghỉ học.");
        }

        if (req.Status != RequestStatus.Submitted && req.Status != RequestStatus.NeedMoreInfo)
        {
            return ApiResponse<bool>.Fail("Chỉ có thể hủy đơn khi đơn đang ở trạng thái Chờ duyệt hoặc Yêu cầu bổ sung thông tin.");
        }

        var oldStatus = req.Status;
        req.Status = RequestStatus.Cancelled;
        req.ProcessedDate = DateTime.UtcNow;
        req.ReviewerNotes = "Phụ huynh tự hủy đơn.";
        await _auditService.LogActionAsync(
            parentUserId,
            AuditActions.LeaveRequestCancel,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            new { Status = oldStatus },
            new { req.Status, Notes = req.ReviewerNotes },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, "Đã hủy đơn xin nghỉ học thành công.");
    }

    public async Task<ApiResponse<bool>> WithdrawLeaveRequestAsync(int requestId, int parentUserId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.LeaveRequest)
            .Include(ar => ar.Student)
            .FirstOrDefaultAsync(ar => ar.Id == requestId && ar.RequestType == RequestTypes.Leave && ar.RequesterUserId == parentUserId, cancellationToken);

        if (req == null || req.LeaveRequest == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn xin nghỉ học.");
        }

        if (req.Status != RequestStatus.Approved)
        {
            return ApiResponse<bool>.Fail("Chỉ có thể thu hồi đơn đã được phê duyệt.");
        }

        var schoolDays = await GetSchoolDaysInRangeAsync(req.LeaveRequest.StartDate, req.LeaveRequest.EndDate, cancellationToken);
        var serviceDayMap = await GetServiceDayMapAsync(req.LeaveRequest.StartDate, req.LeaveRequest.EndDate, cancellationToken);

        var withdrawableDates = schoolDays.Where(d => !IsWithdrawCutoffPassed(d, req.LeaveRequest.Session, serviceDayMap)).ToList();
        if (withdrawableDates.Count == 0)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG27,
                new List<string> { "Tất cả các ngày trong đơn đã qua giờ chốt hoặc đã bị khóa, không thể thu hồi đơn đã duyệt." });
        }

        var cleanReason = string.IsNullOrWhiteSpace(reason) ? "Phụ huynh thu hồi đơn đã duyệt trước giờ chốt." : reason.Trim();
        var attRepo = _unitOfWork.Repository<AttendanceRecord>();
        var lunchRegRepo = _unitOfWork.Repository<LunchRegistration>();

        if (req.StudentId.HasValue)
        {
            var matchingSessions = req.LeaveRequest.Session switch
            {
                LeaveSession.Morning => new[] { AttendanceSession.Morning },
                LeaveSession.Afternoon => new[] { AttendanceSession.Afternoon },
                _ => new[] { AttendanceSession.Morning, AttendanceSession.Afternoon }
            };

            if (attRepo != null)
            {
                var autoRecords = await attRepo.Query()
                    .Where(ar => ar.StudentId == req.StudentId.Value
                              && withdrawableDates.Contains(ar.Date)
                              && matchingSessions.Contains(ar.Session)
                              && ar.LeaveRequestId == req.LeaveRequest.Id
                              && ar.Status == AttendanceStatus.ExcusedAbsence)
                    .ToListAsync(cancellationToken);

                foreach (var ar in autoRecords)
                {
                    await attRepo.DeleteAsync(ar, cancellationToken);
                }
            }

            if (!req.LeaveRequest.KeepLunchService &&
                (req.LeaveRequest.Session == LeaveSession.FullDay || req.LeaveRequest.Session == LeaveSession.Morning))
            {
                foreach (var d in withdrawableDates)
                {
                    var shouldRestore = true;
                    if (lunchRegRepo != null)
                    {
                        var reg = await lunchRegRepo.Query()
                            .FirstOrDefaultAsync(r => r.StudentId == req.StudentId.Value && r.Date == d, cancellationToken);
                        shouldRestore = reg != null && !reg.IsActive && reg.CancellationSource == LunchCancellationSource.Leave;
                    }

                    if (shouldRestore)
                    {
                        await _lunchService.RestoreMealForStudentAsync(
                            req.StudentId.Value,
                            d,
                            $"Phụ huynh thu hồi đơn {req.RequestNumber}",
                            parentUserId,
                            new[] { UserRoles.Parent },
                            cancellationToken);
                    }
                }
            }
        }

        var isPartialWithdraw = withdrawableDates.Count < schoolDays.Count;
        var oldEndDate = req.LeaveRequest.EndDate;
        if (!isPartialWithdraw)
        {
            req.Status = RequestStatus.Cancelled;
            req.ProcessedDate = DateTime.UtcNow;
            req.LeaveRequest.IsLunchCancelled = false;
            req.ReviewerNotes = cleanReason;
        }
        else
        {
            var keptDates = schoolDays.Except(withdrawableDates).OrderBy(d => d).ToList();
            req.LeaveRequest.EndDate = keptDates.Last();
            if (req.StudentId.HasValue && lunchRegRepo != null)
            {
                var hasRemainingCancelledLunch = await lunchRegRepo.Query()
                    .AnyAsync(r => r.StudentId == req.StudentId.Value
                                && keptDates.Contains(r.Date)
                                && !r.IsActive
                                && r.CancellationSource == LunchCancellationSource.Leave, cancellationToken);
                req.LeaveRequest.IsLunchCancelled = hasRemainingCancelledLunch;
            }
            else if (req.LeaveRequest.KeepLunchService || req.LeaveRequest.Session == LeaveSession.Afternoon)
            {
                req.LeaveRequest.IsLunchCancelled = false;
            }
            req.ReviewerNotes = $"{req.ReviewerNotes} | Thu hồi các ngày ({string.Join(", ", withdrawableDates.Select(d => d.ToString("dd-MM-yyyy")))}): {cleanReason}".Trim();
        }

        await _auditService.LogActionAsync(
            parentUserId,
            AuditActions.LeaveRequestWithdraw,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            new { Status = RequestStatus.Approved, EndDate = oldEndDate },
            new { req.Status, EndDate = req.LeaveRequest.EndDate, WithdrawnDates = withdrawableDates.Select(d => d.ToString("yyyy-MM-dd")).ToList(), Notes = cleanReason },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (req.StudentId.HasValue)
        {
            var datesText = string.Join(", ", withdrawableDates.Select(d => d.ToString("dd-MM-yyyy")));
            await NotifyHomeroomTeacherAsync(
                req.StudentId.Value,
                "Phụ huynh đã thu hồi đơn nghỉ đã duyệt",
                $"Phụ huynh học sinh {req.Student?.FullName} đã thu hồi đơn nghỉ {req.RequestNumber} cho ngày ({datesText}) trước giờ chốt. Lý do: {cleanReason}",
                $"/teacher/leave-requests?requestId={req.Id}",
                cancellationToken);
        }

        return ApiResponse<bool>.Ok(true, isPartialWithdraw
            ? "Đã thu hồi các ngày chưa qua giờ chốt của đơn nghỉ và khôi phục suất ăn liên quan."
            : "Đã thu hồi đơn xin nghỉ đã duyệt và khôi phục suất ăn (nếu có) thành công.");
    }

    private async Task<LeaveRequestItemDto?> MapToDtoAsync(int requestId, CancellationToken cancellationToken)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.Student)
            .Include(ar => ar.RequesterUser)
            .Include(ar => ar.ReviewerUser)
            .Include(ar => ar.LeaveRequest)
            .FirstOrDefaultAsync(ar => ar.Id == requestId, cancellationToken);

        if (req == null || req.LeaveRequest == null) return null;

        var list = await MapToDtosAsync(new List<AdministrativeRequest> { req }, cancellationToken);
        return list.FirstOrDefault();
    }

    private async Task<List<LeaveRequestItemDto>> MapToDtosAsync(IReadOnlyList<AdministrativeRequest> requests, CancellationToken cancellationToken)
    {
        var validRequests = requests.Where(r => r.LeaveRequest != null).ToList();
        if (validRequests.Count == 0) return new List<LeaveRequestItemDto>();

        var studentIds = validRequests.Where(r => r.StudentId.HasValue).Select(r => r.StudentId!.Value).Distinct().ToList();
        var classRepo = _unitOfWork.Repository<ClassAssignment>();
        var classMap = new Dictionary<int, ClassAssignment>();
        if (classRepo != null && studentIds.Count > 0)
        {
            var assignments = await classRepo.Query()
                .Include(ca => ca.Class)
                .Where(ca => studentIds.Contains(ca.StudentId) && ca.IsActive)
                .ToListAsync(cancellationToken);

            classMap = assignments
                .GroupBy(ca => ca.StudentId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id).First());
        }

        var minDate = validRequests.Min(r => r.LeaveRequest!.StartDate.Date);
        var maxDate = validRequests.Max(r => r.LeaveRequest!.EndDate.Date);
        var calendarMap = await GetCalendarDayMapAsync(minDate, maxDate, cancellationToken);
        var serviceDayMap = await GetServiceDayMapAsync(minDate, maxDate, cancellationToken);
        var threshold = EscalationThresholdDays;

        // Nạp lịch sử trạng thái từ AuditLogs
        var requestIdStrings = validRequests.Select(r => r.Id.ToString()).ToList();
        var auditRepo = _unitOfWork.Repository<AuditLog>();
        var auditLogsByEntity = new Dictionary<string, List<AuditLog>>();
        var actorNames = new Dictionary<int, string>();

        if (auditRepo != null)
        {
            var logs = await auditRepo.Query()
                .Where(al => al.EntityName == nameof(AdministrativeRequest) && al.EntityId != null && requestIdStrings.Contains(al.EntityId))
                .OrderBy(al => al.Timestamp)
                .ToListAsync(cancellationToken);

            auditLogsByEntity = logs
                .GroupBy(l => l.EntityId!)
                .ToDictionary(g => g.Key, g => g.ToList());

            var actorIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
            var userRepo = _unitOfWork.Repository<User>();
            if (userRepo != null && actorIds.Count > 0)
            {
                var users = await userRepo.Query()
                    .Where(u => actorIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FullName })
                    .ToListAsync(cancellationToken);
                actorNames = users.ToDictionary(u => u.Id, u => u.FullName);
            }
        }

        var list = new List<LeaveRequestItemDto>(validRequests.Count);
        foreach (var req in validRequests)
        {
            var lr = req.LeaveRequest!;
            classMap.TryGetValue(req.StudentId ?? 0, out var currentClass);
            var schoolDays = ExtractSchoolDays(lr.StartDate, lr.EndDate, calendarMap);
            var totalSchoolDays = schoolDays.Count;
            var isEscalated = totalSchoolDays > threshold;
            var canEdit = req.Status == RequestStatus.Submitted || req.Status == RequestStatus.NeedMoreInfo;
            var canWithdraw = req.Status == RequestStatus.Approved && schoolDays.Any(d => !IsWithdrawCutoffPassed(d, lr.Session, serviceDayMap));

            var history = BuildStatusHistory(req, auditLogsByEntity, actorNames);

            list.Add(new LeaveRequestItemDto
            {
                Id = req.Id,
                RequestNumber = req.RequestNumber,
                StudentId = req.StudentId ?? 0,
                StudentName = req.Student?.FullName ?? string.Empty,
                StudentCode = req.Student?.StudentCode ?? string.Empty,
                ClassId = currentClass?.ClassId,
                ClassName = currentClass?.Class?.ClassName ?? string.Empty,
                RequesterUserId = req.RequesterUserId,
                RequesterName = req.RequesterUser?.FullName ?? string.Empty,
                RequesterPhone = req.RequesterUser?.PhoneNumber,
                StartDate = lr.StartDate,
                EndDate = lr.EndDate,
                Session = lr.Session,
                KeepLunchService = lr.KeepLunchService,
                IsLunchCancelled = lr.IsLunchCancelled,
                Reason = lr.Reason,
                DoctorNoteAttachmentUrl = lr.DoctorNoteAttachmentUrl,
                TotalDays = totalSchoolDays,
                IsEscalated = isEscalated,
                CanEdit = canEdit,
                CanWithdraw = canWithdraw,
                Status = req.Status,
                SubmissionDate = req.SubmissionDate,
                ReviewerUserId = req.ReviewerUserId,
                ReviewerName = req.ReviewerUser?.FullName,
                ProcessedDate = req.ProcessedDate,
                ReviewerNotes = req.ReviewerNotes,
                StatusHistory = history
            });
        }

        return list;
    }

    private static List<LeaveStatusHistoryItemDto> BuildStatusHistory(
        AdministrativeRequest req,
        IReadOnlyDictionary<string, List<AuditLog>> logsByEntity,
        IReadOnlyDictionary<int, string> actorNames)
    {
        if (logsByEntity.TryGetValue(req.Id.ToString(), out var logs) && logs.Count > 0)
        {
            return logs.Select(log =>
            {
                var notes = ExtractNotesFromAuditJson(log.NewValues);
                var actorName = log.UserId.HasValue && actorNames.TryGetValue(log.UserId.Value, out var name) ? name : null;
                return new LeaveStatusHistoryItemDto
                {
                    Action = log.Action,
                    ActionLabel = GetActionLabel(log.Action, log.NewValues),
                    ActorName = actorName,
                    Notes = notes,
                    Timestamp = log.Timestamp
                };
            }).ToList();
        }

        // Fallback cho dữ liệu seed chưa có AuditLog
        var fallback = new List<LeaveStatusHistoryItemDto>
        {
            new()
            {
                Action = AuditActions.LeaveRequestSubmit,
                ActionLabel = "Phụ huynh gửi đơn",
                ActorName = req.RequesterUser?.FullName,
                Notes = req.LeaveRequest?.Reason,
                Timestamp = req.SubmissionDate
            }
        };

        if (req.ProcessedDate.HasValue)
        {
            fallback.Add(new LeaveStatusHistoryItemDto
            {
                Action = req.Status switch
                {
                    RequestStatus.Approved => AuditActions.LeaveRequestApprove,
                    RequestStatus.Rejected => AuditActions.LeaveRequestReject,
                    RequestStatus.UnderReview => AuditActions.LeaveRequestForward,
                    RequestStatus.NeedMoreInfo => AuditActions.LeaveRequestRequestMoreInfo,
                    RequestStatus.Cancelled => AuditActions.LeaveRequestCancel,
                    _ => "PROCESSED"
                },
                ActionLabel = req.Status switch
                {
                    RequestStatus.Approved => "Đã phê duyệt",
                    RequestStatus.Rejected => "Từ chối đơn",
                    RequestStatus.UnderReview => "GVCN chuyển Ban Giám Hiệu",
                    RequestStatus.NeedMoreInfo => "Yêu cầu bổ sung thông tin",
                    RequestStatus.Cancelled => "Đã hủy",
                    _ => req.Status.ToString()
                },
                ActorName = req.ReviewerUser?.FullName,
                Notes = req.ReviewerNotes,
                Timestamp = req.ProcessedDate.Value
            });
        }

        return fallback;
    }

    private static string GetActionLabel(string action, string? newValuesJson) => action switch
    {
        AuditActions.LeaveRequestSubmit => "Phụ huynh gửi đơn",
        AuditActions.LeaveRequestResubmit => "Phụ huynh cập nhật & gửi lại đơn",
        AuditActions.LeaveRequestForward => "GVCN chuyển Ban Giám Hiệu",
        AuditActions.LeaveRequestRequestMoreInfo => "Yêu cầu bổ sung thông tin",
        AuditActions.LeaveRequestApprove => newValuesJson != null && newValuesJson.Contains("SchoolBoard", StringComparison.OrdinalIgnoreCase)
            ? "Ban Giám Hiệu phê duyệt"
            : "GVCN phê duyệt",
        AuditActions.LeaveRequestReject => newValuesJson != null && newValuesJson.Contains("SchoolBoard", StringComparison.OrdinalIgnoreCase)
            ? "Ban Giám Hiệu từ chối"
            : "GVCN từ chối",
        AuditActions.LeaveRequestCancel => "Phụ huynh hủy đơn",
        AuditActions.LeaveRequestWithdraw => "Phụ huynh thu hồi đơn đã duyệt",
        _ => action
    };

    private static string? ExtractNotesFromAuditJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("Notes", out var notesProp) && notesProp.ValueKind == JsonValueKind.String)
                return notesProp.GetString();
            if (root.TryGetProperty("Reason", out var reasonProp) && reasonProp.ValueKind == JsonValueKind.String)
                return reasonProp.GetString();
        }
        catch
        {
            // Ignore malformed JSON in legacy audit entries
        }
        return null;
    }

    public async Task<ApiResponse<AttachmentUploadResultDto>> UploadAttachmentAsync(Stream content, string fileName, long length, CancellationToken cancellationToken = default)
    {
        if (length <= 0)
        {
            return ApiResponse<AttachmentUploadResultDto>.Fail("Vui lòng chọn tệp tin cần tải lên.");
        }

        if (length > MaxAttachmentBytes)
        {
            return ApiResponse<AttachmentUploadResultDto>.Fail("Kích thước tệp không được vượt quá 10MB.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedAttachmentExtensions.Contains(ext))
        {
            return ApiResponse<AttachmentUploadResultDto>.Fail(
                SystemMessages.MSG41,
                new List<string> { "Định dạng tệp không được hỗ trợ theo quy định (BR-50). Vui lòng chọn tệp PDF, JPG hoặc PNG." });
        }

        var relativePath = await _fileStorage.SaveFileAsync(content, fileName, "attachments", cancellationToken);
        var normalizedPath = relativePath.Replace('\\', '/');
        var fileUrl = $"{AttachmentUrlPrefix}{Uri.EscapeDataString(normalizedPath)}";

        return ApiResponse<AttachmentUploadResultDto>.Ok(new AttachmentUploadResultDto
        {
            Url = fileUrl,
            FileName = fileName,
            FileSize = length,
            SavedPath = normalizedPath
        }, "Tải lên tệp đính kèm thành công.");
    }

    public async Task<AttachmentFileDto?> GetAttachmentAsync(
        string path,
        int? requestingUserId = null,
        IReadOnlyList<UserRoles>? requestingRoles = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var decoded = Uri.UnescapeDataString(path).Trim();
        if (string.IsNullOrWhiteSpace(decoded)
            || Path.IsPathRooted(decoded)
            || decoded.Contains("..", StringComparison.Ordinal)
            || decoded.Contains(':'))
        {
            return null;
        }

        var normalizedPath = decoded.Replace('\\', '/').TrimStart('/');
        if (!normalizedPath.StartsWith("attachments/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var ext = Path.GetExtension(normalizedPath).ToLowerInvariant();
        if (!AllowedAttachmentExtensions.Contains(ext))
        {
            return null;
        }

        if (requestingUserId.HasValue)
        {
            if (requestingUserId.Value <= 0)
            {
                return null;
            }

            var escapedPath = Uri.EscapeDataString(normalizedPath);
            var reqRepo = _unitOfWork.Repository<AdministrativeRequest>();
            if (reqRepo != null)
            {
                var matchingReq = await reqRepo.Query()
                    .Include(ar => ar.LeaveRequest)
                    .FirstOrDefaultAsync(ar => ar.RequestType == RequestTypes.Leave
                        && ar.LeaveRequest != null
                        && ar.LeaveRequest.DoctorNoteAttachmentUrl != null
                        && (ar.LeaveRequest.DoctorNoteAttachmentUrl.Contains(escapedPath)
                            || ar.LeaveRequest.DoctorNoteAttachmentUrl.Contains(normalizedPath)), cancellationToken);

                if (matchingReq != null)
                {
                    var canAccess = await CanAccessLeaveRequestAsync(matchingReq, requestingUserId.Value, requestingRoles, cancellationToken);
                    if (!canAccess)
                    {
                        return null;
                    }
                }
            }
        }

        var stream = await _fileStorage.GetFileAsync(normalizedPath, cancellationToken);
        if (stream == null)
        {
            return null;
        }

        return new AttachmentFileDto
        {
            Content = stream,
            FileName = Path.GetFileName(normalizedPath),
            ContentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            }
        };
    }
}
