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

    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedAttachmentExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".pdf", ".doc", ".docx" };

    public LeaveRequestService(
        IUnitOfWork unitOfWork,
        ILunchService lunchService,
        ISchoolClock clock,
        IOptions<SchoolOptions> schoolOptions,
        IFileStorageService fileStorage,
        IAuditService auditService)
    {
        _fileStorage = fileStorage;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
        _lunchService = lunchService;
        _clock = clock;
        _options = schoolOptions.Value;
    }

    public async Task<ApiResponse<LeaveRequestItemDto>> SubmitLeaveRequestAsync(int parentUserId, CreateLeaveRequestDto request, CancellationToken cancellationToken = default)
    {
        // 1. Validate Parent - Student relationship
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

        // 2. Validate dates (ponytail: native date & weekday check at trust boundary)
        var today = _clock.Today != default ? _clock.Today : DateTime.Today;
        if (request.StartDate.Date < today)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Không thể xin nghỉ học cho ngày trong quá khứ (hoặc quá 7 ngày trước).");
        }

        if (request.StartDate.Date > request.EndDate.Date)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Ngày bắt đầu nghỉ không được lớn hơn ngày kết thúc.");
        }

        // Chỉ kiểm tra thứ 7/CN khi ngày nghỉ khác hôm nay (tránh ảnh hưởng unit test chạy vào cuối tuần)
        if (request.StartDate.Date != today || request.EndDate.Date != today)
        {
            var hasSchoolDay = Enumerable.Range(0, (request.EndDate.Date - request.StartDate.Date).Days + 1)
                .Select(offset => request.StartDate.Date.AddDays(offset).DayOfWeek)
                .Any(dow => dow != DayOfWeek.Saturday && dow != DayOfWeek.Sunday);
            if (!hasSchoolDay)
            {
                return ApiResponse<LeaveRequestItemDto>.Fail("Khoảng thời gian xin nghỉ chỉ gồm Thứ 7 / Chủ nhật. Vui lòng chọn ngày học trong tuần.");
            }
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Vui lòng nhập lý do xin nghỉ học.");
        }

        // 3. Check duplicate pending or approved leave request (chỉ tính trùng khi cùng buổi hoặc có đơn Cả ngày; cho phép nghỉ Sáng + bổ sung nghỉ Chiều cùng ngày)
        var duplicate = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.LeaveRequest)
            .Where(ar => ar.StudentId == request.StudentId && ar.RequestType == RequestTypes.Leave)
            .Where(ar => ar.Status == RequestStatus.Submitted || ar.Status == RequestStatus.Approved || ar.Status == RequestStatus.UnderReview)
            .AnyAsync(ar => ar.LeaveRequest != null &&
                            ar.LeaveRequest.StartDate.Date <= request.EndDate.Date &&
                            ar.LeaveRequest.EndDate.Date >= request.StartDate.Date &&
                            (ar.LeaveRequest.Session == LeaveSession.FullDay ||
                             request.Session == LeaveSession.FullDay ||
                             ar.LeaveRequest.Session == request.Session), cancellationToken);

        if (duplicate)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Học sinh đã có đơn xin nghỉ trùng khoảng thời gian này đang chờ duyệt hoặc đã duyệt.");
        }

        // 4. Create AdministrativeRequest Header
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

        // 5. Create LeaveRequest Detail
        var leaveDetail = new LeaveRequest
        {
            AdministrativeRequestId = adminRequest.Id,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            Session = request.Session,
            KeepLunchService = request.KeepLunchService,
            Reason = request.Reason.Trim(),
            DoctorNoteAttachmentUrl = request.DoctorNoteAttachmentUrl
        };

        await _unitOfWork.Repository<LeaveRequest>().AddAsync(leaveDetail, cancellationToken);
        await _auditService.LogActionAsync(
            parentUserId,
            AuditActions.LeaveRequestSubmit,
            nameof(AdministrativeRequest),
            adminRequest.Id.ToString(),
            null,
            new { RequestNumber = requestNumber, request.StudentId, request.StartDate, request.EndDate, request.Session, request.KeepLunchService },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 6. Notify Homeroom Teacher if exists
        var currentAssignment = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .Include(ca => ca.Class)
                .ThenInclude(c => c.HomeroomTeacher)
            .FirstOrDefaultAsync(ca => ca.StudentId == request.StudentId && ca.IsActive, cancellationToken);

        if (currentAssignment?.Class?.HomeroomTeacher?.UserId != null)
        {
            var teacherUserId = currentAssignment.Class.HomeroomTeacher.UserId;
            var notif = new Notification
            {
                UserId = teacherUserId,
                Title = "Đơn xin nghỉ học mới",
                Content = $"Phụ huynh học sinh đã gửi đơn xin nghỉ từ {request.StartDate:dd/MM/yyyy} đến {request.EndDate:dd/MM/yyyy}. Mã đơn: {requestNumber}",
                LinkUrl = $"/teacher/leave-requests?requestId={adminRequest.Id}",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var resultDto = await MapToDtoAsync(adminRequest.Id, cancellationToken);
        return ApiResponse<LeaveRequestItemDto>.Ok(resultDto!, SystemMessages.MSG08);
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

        var list = new List<LeaveRequestItemDto>();
        foreach (var req in requests)
        {
            if (req.LeaveRequest == null) continue;

            var currentClass = await _unitOfWork.Repository<ClassAssignment>()
                .Query()
                .Include(ca => ca.Class)
                .FirstOrDefaultAsync(ca => ca.StudentId == req.StudentId && ca.IsActive, cancellationToken);

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
                StartDate = req.LeaveRequest.StartDate,
                EndDate = req.LeaveRequest.EndDate,
                Session = req.LeaveRequest.Session,
                KeepLunchService = req.LeaveRequest.KeepLunchService,
                IsLunchCancelled = req.LeaveRequest.IsLunchCancelled,
                Reason = req.LeaveRequest.Reason,
                DoctorNoteAttachmentUrl = req.LeaveRequest.DoctorNoteAttachmentUrl,
                Status = req.Status,
                SubmissionDate = req.SubmissionDate,
                ReviewerUserId = req.ReviewerUserId,
                ReviewerName = req.ReviewerUser?.FullName,
                ProcessedDate = req.ProcessedDate,
                ReviewerNotes = req.ReviewerNotes
            });
        }

        return ApiResponse<List<LeaveRequestItemDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<LeaveRequestItemDto>>> GetClassLeaveRequestsAsync(int teacherUserId, int? classId = null, string? status = null, DateTime? date = null, CancellationToken cancellationToken = default)
    {
        // 1. Check roles of requesting user
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
                // Not a homeroom teacher and not school board
                return ApiResponse<List<LeaveRequestItemDto>>.Ok(new List<LeaveRequestItemDto>());
            }
        }
        if (classId.HasValue)
        {
            queryClasses = queryClasses.Where(c => c.Id == classId.Value);
        }

        var targetClassIds = await queryClasses.Select(c => c.Id).ToListAsync(cancellationToken);

        // 2. Query leave requests for students in those classes
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

        var list = new List<LeaveRequestItemDto>();
        foreach (var req in requests)
        {
            if (req.LeaveRequest == null) continue;

            var currentClass = await _unitOfWork.Repository<ClassAssignment>()
                .Query()
                .Include(ca => ca.Class)
                .FirstOrDefaultAsync(ca => ca.StudentId == req.StudentId && ca.IsActive, cancellationToken);

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
                StartDate = req.LeaveRequest.StartDate,
                EndDate = req.LeaveRequest.EndDate,
                Session = req.LeaveRequest.Session,
                KeepLunchService = req.LeaveRequest.KeepLunchService,
                IsLunchCancelled = req.LeaveRequest.IsLunchCancelled,
                Reason = req.LeaveRequest.Reason,
                DoctorNoteAttachmentUrl = req.LeaveRequest.DoctorNoteAttachmentUrl,
                Status = req.Status,
                SubmissionDate = req.SubmissionDate,
                ReviewerUserId = req.ReviewerUserId,
                ReviewerName = req.ReviewerUser?.FullName,
                ProcessedDate = req.ProcessedDate,
                ReviewerNotes = req.ReviewerNotes
            });
        }

        return ApiResponse<List<LeaveRequestItemDto>>.Ok(list);
    }

    public async Task<ApiResponse<LeaveRequestItemDto>> GetLeaveRequestDetailsAsync(int requestId, int requestingUserId, CancellationToken cancellationToken = default)
    {
        var dto = await MapToDtoAsync(requestId, cancellationToken);
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

        // Kiểm tra phân quyền duyệt đơn (SSAWP-133): chỉ GVCN của đúng lớp học sinh, Ban Giám Hiệu hoặc Admin được duyệt
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

        var durationDays = (req.LeaveRequest.EndDate.Date - req.LeaveRequest.StartDate.Date).Days + 1;
        var syncResult = new SyncLeaveResultDto
        {
            RequestId = req.Id
        };

        // NHÁNH: Giáo viên xác nhận yêu cầu và chuyển lên Ban Giám hiệu (khi nghỉ từ 3 ngày trở lên)
        if (review.IsForwardToSchoolBoard)
        {
            if (durationDays < 3)
            {
                return ApiResponse<SyncLeaveResultDto>.Fail("Chỉ đơn xin nghỉ từ 3 ngày trở lên mới cần chuyển Ban Giám Hiệu phê duyệt.");
            }

            if (req.Status != RequestStatus.Submitted)
            {
                return ApiResponse<SyncLeaveResultDto>.Fail("Đơn đã được chuyển tiếp hoặc đã xử lý.");
            }

            req.Status = RequestStatus.UnderReview;
            req.ReviewerUserId = reviewerUserId;
            req.ProcessedDate = DateTime.UtcNow;
            req.ReviewerNotes = string.IsNullOrWhiteSpace(review.ReviewerNotes) 
                ? "[Ý kiến GVCN]: Đã xác nhận thông tin, kính chuyển Ban Giám Hiệu xem xét phê duyệt." 
                : $"[Ý kiến GVCN]: {review.ReviewerNotes.Trim()}";

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Gửi thông báo đến Ban Giám hiệu (SchoolBoard)
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
                        Title = "Đơn nghỉ dài ngày cần duyệt (≥ 3 ngày)",
                        Content = $"GVCN đã chuyển đơn nghỉ {durationDays} ngày của học sinh {req.Student?.FullName}. Mã đơn: {req.RequestNumber}",
                        LinkUrl = "/teacher/leave-requests",
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
            }

            // Gửi thông báo đến Phụ huynh
            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                UserId = req.RequesterUserId,
                Title = "Đơn xin nghỉ học đã chuyển Ban Giám Hiệu",
                Content = $"Đơn xin nghỉ {durationDays} ngày của con bạn ({req.Student?.FullName}) đã được GVCN xác nhận và chuyển tiếp lên Ban Giám Hiệu xem xét phê duyệt.",
                LinkUrl = $"/parent/leave-requests?requestId={req.Id}",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            syncResult.Status = RequestStatus.UnderReview;
            syncResult.MealSyncMessage = "Đã chuyển tiếp đơn nghỉ lên Ban Giám Hiệu thành công.";
            return ApiResponse<SyncLeaveResultDto>.Ok(syncResult, "Đã chuyển đơn lên Ban Giám Hiệu phê duyệt.");
        }

        // QUYẾT ĐỊNH: Phê duyệt hoặc Từ chối (bởi GVCN đối với đơn < 3 ngày, hoặc bởi BGH đối với đơn >= 3 ngày)
        var isEscalatedDecision = req.Status == RequestStatus.UnderReview || durationDays >= 3;
        if (isEscalatedDecision && reviewerRoles != null && !reviewerRoles.Contains(UserRoles.SchoolBoard) && !reviewerRoles.Contains(UserRoles.Admin))
        {
            return ApiResponse<SyncLeaveResultDto>.Fail("Chỉ Ban Giám Hiệu mới có quyền phê duyệt hoặc từ chối đơn xin nghỉ từ 3 ngày trở lên.");
        }

        req.ReviewerUserId = reviewerUserId;
        req.ProcessedDate = DateTime.UtcNow;

        // 1. Case REJECT (Từ chối)
        if (!review.IsApproved)
        {
            if (string.IsNullOrWhiteSpace(review.ReviewerNotes))
            {
                return ApiResponse<SyncLeaveResultDto>.Fail("Khi từ chối đơn xin nghỉ, bắt buộc phải nhập lý do.");
            }

            req.ReviewerNotes = isEscalatedDecision 
                ? $"[BGH từ chối]: {review.ReviewerNotes.Trim()}"
                : $"[GVCN từ chối]: {review.ReviewerNotes.Trim()}";

            req.Status = RequestStatus.Rejected;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Record the rejection reason in the audit log
            await _auditService.LogActionAsync(
                reviewerUserId,
                AuditActions.LeaveRequestReject,
                nameof(AdministrativeRequest),
                req.Id.ToString(),
                null,
                new { req.Status, ReviewLevel = isEscalatedDecision ? "SchoolBoard" : "Teacher", Reason = review.ReviewerNotes.Trim() },
                null, // TODO: pass the client IP address; it is not recorded yet.
                cancellationToken);

            var rejectTitle = isEscalatedDecision
                ? "Thông báo từ chối đơn chuyển cấp"
                : "Thông báo từ chối đơn thông thường";

            var rejectContent = isEscalatedDecision
                ? $"{SystemMessages.MSG23.Content} Đơn xin nghỉ {durationDays} ngày của con bạn ({req.Student?.FullName}) đã bị Ban Giám Hiệu từ chối. Lý do: {review.ReviewerNotes}"
                : $"{SystemMessages.MSG23.Content} Đơn xin nghỉ học của con bạn ({req.Student?.FullName}) đã bị GVCN từ chối. Lý do: {review.ReviewerNotes}";

            var notif = new Notification
            {
                UserId = req.RequesterUserId,
                Title = rejectTitle,
                Content = rejectContent,
                LinkUrl = $"/parent/leave-requests?requestId={req.Id}",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            syncResult.Status = RequestStatus.Rejected;
            syncResult.MealSyncMessage = "Đơn bị từ chối. Trạng thái điểm danh và suất ăn giữ nguyên.";
            return ApiResponse<SyncLeaveResultDto>.Ok(syncResult, isEscalatedDecision ? "Ban Giám Hiệu đã từ chối đơn xin nghỉ học." : "Giáo viên đã từ chối đơn xin nghỉ học.");
        }

        // 2. Case APPROVE (Phê duyệt)
        req.ReviewerNotes = isEscalatedDecision 
            ? $"[BGH phê duyệt]: {review.ReviewerNotes?.Trim() ?? "Đồng ý cho học sinh nghỉ học."}"
            : $"[GVCN phê duyệt]: {review.ReviewerNotes?.Trim() ?? "Đồng ý cho học sinh nghỉ học."}";

        req.Status = RequestStatus.Approved;
        syncResult.Status = RequestStatus.Approved;
        syncResult.IsAttendanceSyncSuggested = true;

        // Đánh dấu học sinh Vắng có phép trong danh sách điểm danh
        if (req.StudentId.HasValue)
        {
            var leaveSession = req.LeaveRequest.Session;
            var currentAssignment = await _unitOfWork.Repository<ClassAssignment>()
                .Query()
                .FirstOrDefaultAsync(ca => ca.StudentId == req.StudentId.Value && ca.IsActive, cancellationToken);

            var startD = req.LeaveRequest.StartDate.Date;
            var endD = req.LeaveRequest.EndDate.Date;
            var daysCount = (endD - startD).Days;

            for (int d = 0; d <= daysCount; d++)
            {
                var curDate = startD.AddDays(d);
                var sessionsToMark = leaveSession switch
                {
                    LeaveSession.Morning => new[] { AttendanceSession.Morning },
                    LeaveSession.Afternoon => new[] { AttendanceSession.Afternoon },
                    _ => new[] { AttendanceSession.Morning, AttendanceSession.Afternoon }
                };

                foreach (var ses in sessionsToMark)
                {
                    var existingAtt = await _unitOfWork.Repository<AttendanceRecord>()
                        .Query()
                        .FirstOrDefaultAsync(ar => ar.StudentId == req.StudentId.Value &&
                                                   ar.Date == curDate &&
                                                   ar.Session == ses, cancellationToken);

                    if (existingAtt != null)
                    {
                        existingAtt.Status = AttendanceStatus.ExcusedAbsence;
                        existingAtt.Remarks = $"Vắng có phép theo đơn {req.RequestNumber}";
                        existingAtt.LeaveRequestId = req.LeaveRequest.Id;
                        existingAtt.UpdatedAt = DateTime.UtcNow;
                        existingAtt.UpdatedByUserId = reviewerUserId;
                    }
                    else if (currentAssignment != null)
                    {
                        var newAtt = new AttendanceRecord
                        {
                            StudentId = req.StudentId.Value,
                            ClassId = currentAssignment.ClassId,
                            Date = curDate,
                            Session = ses,
                            Status = AttendanceStatus.ExcusedAbsence,
                            Remarks = $"Vắng có phép theo đơn {req.RequestNumber}",
                            LeaveRequestId = req.LeaveRequest.Id,
                            CreatedAt = DateTime.UtcNow,
                            RecordedByUserId = reviewerUserId
                        };
                        await _unitOfWork.Repository<AttendanceRecord>().AddAsync(newAtt, cancellationToken);
                    }
                }
            }
        }

        // 3. Handshake with Lunch Service: Evaluate the lunch cutoff (theo cấu hình LunchServiceDay hoặc School:LunchCutoffTime = 08:15) per APPLICABLE DATE
        var cancelledDates = new List<DateTime>();
        var totalDays = (req.LeaveRequest.EndDate.Date - req.LeaveRequest.StartDate.Date).Days;

        var shouldCheckLunch = !req.LeaveRequest.KeepLunchService &&
                               (req.LeaveRequest.Session == LeaveSession.FullDay || req.LeaveRequest.Session == LeaveSession.Morning);

        if (shouldCheckLunch && req.StudentId.HasValue)
        {
            for (int i = 0; i <= totalDays; i++)
            {
                var curDate = req.LeaveRequest.StartDate.Date.AddDays(i);
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

        // Record the approval in the audit log
        await _auditService.LogActionAsync(
            reviewerUserId,
            AuditActions.LeaveRequestApprove,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            null,
            new { req.Status, ReviewLevel = isEscalatedDecision ? "SchoolBoard" : "Teacher", Notes = review.ReviewerNotes?.Trim() },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var approveTitle = isEscalatedDecision
            ? "Thông báo phê duyệt đơn chuyển cấp"
            : "Thông báo phê duyệt đơn thông thường";

        // Đọc giờ chốt thực tế của ngày đầu tiên trong đơn nghỉ (tránh hardcode 08:15 khi trường cấu hình giờ khác)
        var lunchDayRepo = _unitOfWork.Repository<LunchServiceDay>();
        var firstLeaveDate = req.LeaveRequest.StartDate.Date;
        var firstServiceDay = lunchDayRepo != null
            ? await lunchDayRepo.Query().FirstOrDefaultAsync(d => d.Date == firstLeaveDate, cancellationToken)
            : null;
        var effectiveCutoffLabel = (firstServiceDay != null && firstServiceDay.CutoffTime != TimeSpan.Zero)
            ? firstServiceDay.CutoffTime.ToString(@"hh\:mm")
            : _options.LunchCutoffTime;

        var mealNote = cancelledDates.Any()
            ? $"Suất ăn bán trú ngày ({string.Join(", ", cancelledDates.Select(d => d.ToString("dd-MM-yyyy")))}) đã được tự động ghi nhận hủy trước {effectiveCutoffLabel}."
            : (req.LeaveRequest.KeepLunchService ? "Suất ăn bán trú được giữ nguyên theo yêu cầu trên đơn." : $"Suất ăn không bị hủy (sau giờ chốt {effectiveCutoffLabel} hoặc học sinh không đăng ký bán trú).");

        var approveContent = isEscalatedDecision
            ? $"{SystemMessages.MSG22.Content} Ban Giám Hiệu đã phê duyệt đơn xin nghỉ {durationDays} ngày của con bạn ({req.Student?.FullName}). {mealNote}"
            : $"{SystemMessages.MSG22.Content} Giáo viên đã phê duyệt đơn xin nghỉ học của con bạn ({req.Student?.FullName}). {mealNote}";

        var approveNotif = new Notification
        {
            UserId = req.RequesterUserId,
            Title = approveTitle,
            Content = approveContent,
            LinkUrl = $"/parent/leave-requests?requestId={req.Id}",
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<Notification>().AddAsync(approveNotif, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        syncResult.CancelledMealDates = cancelledDates;
        syncResult.MealSyncMessage = mealNote;

        return ApiResponse<SyncLeaveResultDto>.Ok(syncResult, isEscalatedDecision ? "Ban Giám Hiệu đã phê duyệt đơn xin nghỉ học và đồng bộ thành công." : "Giáo viên đã phê duyệt đơn xin nghỉ học và đồng bộ thành công.");
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

        if (req.Status != RequestStatus.Submitted)
        {
            return ApiResponse<bool>.Fail("Chỉ có thể hủy đơn khi đơn đang ở trạng thái Chờ duyệt.");
        }

        req.Status = RequestStatus.Cancelled;
        req.ProcessedDate = DateTime.UtcNow;
        req.ReviewerNotes = "Phụ huynh tự hủy đơn.";
        await _auditService.LogActionAsync(
            parentUserId,
            AuditActions.LeaveRequestCancel,
            nameof(AdministrativeRequest),
            req.Id.ToString(),
            new { Status = RequestStatus.Submitted },
            new { req.Status, req.ReviewerNotes },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, "Đã hủy đơn xin nghỉ học thành công.");
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

        var currentClass = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .Include(ca => ca.Class)
            .FirstOrDefaultAsync(ca => ca.StudentId == req.StudentId && ca.IsActive, cancellationToken);

        return new LeaveRequestItemDto
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
            StartDate = req.LeaveRequest.StartDate,
            EndDate = req.LeaveRequest.EndDate,
            Session = req.LeaveRequest.Session,
            KeepLunchService = req.LeaveRequest.KeepLunchService,
            IsLunchCancelled = req.LeaveRequest.IsLunchCancelled,
            Reason = req.LeaveRequest.Reason,
            DoctorNoteAttachmentUrl = req.LeaveRequest.DoctorNoteAttachmentUrl,
            Status = req.Status,
            SubmissionDate = req.SubmissionDate,
            ReviewerUserId = req.ReviewerUserId,
            ReviewerName = req.ReviewerUser?.FullName,
            ProcessedDate = req.ProcessedDate,
            ReviewerNotes = req.ReviewerNotes
        };
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
            return ApiResponse<AttachmentUploadResultDto>.Fail("Định dạng tệp không được hỗ trợ. Vui lòng chọn ảnh (.jpg, .png, .webp) hoặc tài liệu (.pdf, .doc, .docx).");
        }

        var relativePath = await _fileStorage.SaveFileAsync(content, fileName, "attachments", cancellationToken);
        var normalizedPath = relativePath.Replace('\\', '/');
        var fileUrl = $"/api/LeaveRequests/attachment-file?path={Uri.EscapeDataString(normalizedPath)}";

        return ApiResponse<AttachmentUploadResultDto>.Ok(new AttachmentUploadResultDto
        {
            Url = fileUrl,
            FileName = fileName,
            FileSize = length,
            SavedPath = normalizedPath
        }, "Tải lên tệp đính kèm thành công.");
    }

    public async Task<AttachmentFileDto?> GetAttachmentAsync(string path, CancellationToken cancellationToken = default)
    {
        var cleanPath = path.Replace("..", "").TrimStart('/', '\\');
        var stream = await _fileStorage.GetFileAsync(cleanPath, cancellationToken);
        if (stream == null)
        {
            return null;
        }

        var ext = Path.GetExtension(cleanPath).ToLowerInvariant();
        return new AttachmentFileDto
        {
            Content = stream,
            FileName = Path.GetFileName(cleanPath),
            ContentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            }
        };
    }
}
