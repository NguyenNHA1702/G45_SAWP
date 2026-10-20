using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILunchService _lunchService;

    public LeaveRequestService(IUnitOfWork unitOfWork, ILunchService lunchService)
    {
        _unitOfWork = unitOfWork;
        _lunchService = lunchService;
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

        // 2. Validate dates
        if (request.StartDate.Date < DateTime.Today.AddDays(-7))
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Không thể xin nghỉ học cho ngày đã quá 7 ngày trước.");
        }

        if (request.StartDate.Date > request.EndDate.Date)
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Ngày bắt đầu nghỉ không được lớn hơn ngày kết thúc.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<LeaveRequestItemDto>.Fail("Vui lòng nhập lý do xin nghỉ học.");
        }

        // 3. Check duplicate pending or approved leave request
        var duplicate = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.LeaveRequest)
            .Where(ar => ar.StudentId == request.StudentId && ar.RequestType == "LeaveRequest")
            .Where(ar => ar.Status == RequestStatus.Submitted || ar.Status == RequestStatus.Approved || ar.Status == RequestStatus.UnderReview)
            .AnyAsync(ar => ar.LeaveRequest != null &&
                            ar.LeaveRequest.StartDate.Date <= request.EndDate.Date &&
                            ar.LeaveRequest.EndDate.Date >= request.StartDate.Date, cancellationToken);

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
            RequestType = "LeaveRequest",
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
            .Where(ar => ar.RequesterUserId == parentUserId && ar.RequestType == "LeaveRequest")
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
        if (classId.HasValue)
        {
            queryClasses = queryClasses.Where(c => c.Id == classId.Value);
        }
        else if (!isSchoolBoardOrAdmin)
        {
            var teacherProfile = await _unitOfWork.Repository<StaffProfile>()
                .Query()
                .FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken);

            if (teacherProfile != null)
            {
                queryClasses = queryClasses.Where(c => c.HomeroomTeacherId == teacherProfile.Id);
            }
            else
            {
                // Not a homeroom teacher and not school board
                return ApiResponse<List<LeaveRequestItemDto>>.Ok(new List<LeaveRequestItemDto>());
            }
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
            .Where(ar => ar.RequestType == "LeaveRequest" && ar.StudentId.HasValue && activeStudentIds.Contains(ar.StudentId.Value));

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

    public async Task<ApiResponse<SyncLeaveResultDto>> ReviewLeaveRequestAsync(int requestId, int reviewerUserId, ReviewLeaveRequestDto review, CancellationToken cancellationToken = default)
    {
        var req = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.LeaveRequest)
            .Include(ar => ar.Student)
            .FirstOrDefaultAsync(ar => ar.Id == requestId && ar.RequestType == "LeaveRequest", cancellationToken);

        if (req == null || req.LeaveRequest == null)
        {
            return ApiResponse<SyncLeaveResultDto>.Fail("Không tìm thấy đơn xin nghỉ học.");
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
        req.ReviewerUserId = reviewerUserId;
        req.ProcessedDate = DateTime.UtcNow;
        var isEscalatedDecision = req.Status == RequestStatus.UnderReview || durationDays >= 3;

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

            // Ghi lý do từ chối vào nhật ký hệ thống (AuditLog)
            var audit = new AuditLog
            {
                UserId = reviewerUserId,
                Action = isEscalatedDecision ? "SchoolBoard_Reject_LeaveRequest" : "Teacher_Reject_LeaveRequest",
                EntityName = "AdministrativeRequest",
                EntityId = req.Id.ToString(),
                NewValues = $"Lý do từ chối: {review.ReviewerNotes.Trim()}",
                Timestamp = DateTime.UtcNow
            };
            await _unitOfWork.Repository<AuditLog>().AddAsync(audit, cancellationToken);

            var rejectTitle = isEscalatedDecision
                ? "Thông báo từ chối đơn chuyển cấp"
                : "Thông báo từ chối đơn thông thường";

            var rejectContent = isEscalatedDecision
                ? $"Đơn xin nghỉ {durationDays} ngày của con bạn ({req.Student?.FullName}) đã bị Ban Giám Hiệu từ chối. Lý do: {review.ReviewerNotes}"
                : $"Đơn xin nghỉ học của con bạn ({req.Student?.FullName}) đã bị GVCN từ chối. Lý do: {review.ReviewerNotes}";

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

        // Đánh dấu học sinh Vắng có phép trong danh sách điểm danh (nếu bản ghi ngày đó đã được lưu trước đó)
        if (req.StudentId.HasValue)
        {
            var existingAttendanceRecords = await _unitOfWork.Repository<AttendanceRecord>()
                .Query()
                .Where(ar => ar.StudentId == req.StudentId.Value &&
                             ar.Date >= req.LeaveRequest.StartDate.Date &&
                             ar.Date <= req.LeaveRequest.EndDate.Date)
                .ToListAsync(cancellationToken);

            foreach (var att in existingAttendanceRecords)
            {
                att.Status = AttendanceStatus.ExcusedAbsence;
                att.Remarks = $"Vắng có phép theo đơn {req.RequestNumber}";
            }
        }

        // 3. Handshake with Lunch Service: Evaluate 08h15 per APPLICABLE DATE
        var cancelledDates = new List<DateTime>();
        var totalDays = (req.LeaveRequest.EndDate.Date - req.LeaveRequest.StartDate.Date).Days;

        var shouldCheckLunch = !req.LeaveRequest.KeepLunchService &&
                               (req.LeaveRequest.Session == LeaveSession.FullDay || req.LeaveRequest.Session == LeaveSession.Morning);

        if (shouldCheckLunch && req.StudentId.HasValue)
        {
            for (int i = 0; i <= totalDays; i++)
            {
                var curDate = req.LeaveRequest.StartDate.Date.AddDays(i);
                var cutoffTime = curDate.Date.Add(new TimeSpan(8, 15, 0));
                var now = DateTime.Now;

                if (now <= cutoffTime)
                {
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
        }

        req.LeaveRequest.IsLunchCancelled = cancelledDates.Any();

        // Ghi nhật ký phê duyệt vào AuditLog
        var approveAudit = new AuditLog
        {
            UserId = reviewerUserId,
            Action = isEscalatedDecision ? "SchoolBoard_Approve_LeaveRequest" : "Teacher_Approve_LeaveRequest",
            EntityName = "AdministrativeRequest",
            EntityId = req.Id.ToString(),
            NewValues = $"Đã phê duyệt. Ghi chú: {review.ReviewerNotes?.Trim() ?? "Đồng ý"}",
            Timestamp = DateTime.UtcNow
        };
        await _unitOfWork.Repository<AuditLog>().AddAsync(approveAudit, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var approveTitle = isEscalatedDecision
            ? "Thông báo phê duyệt đơn chuyển cấp"
            : "Thông báo phê duyệt đơn thông thường";

        var mealNote = cancelledDates.Any()
            ? $"Suất ăn bán trú ngày ({string.Join(", ", cancelledDates.Select(d => d.ToString("dd/MM")))}) đã được tự động ghi nhận hủy trước 08h15."
            : (req.LeaveRequest.KeepLunchService ? "Suất ăn bán trú được giữ nguyên theo yêu cầu trên đơn." : "Suất ăn không bị hủy (sau giờ chốt 08h15 hoặc học sinh không đăng ký bán trú).");

        var approveContent = isEscalatedDecision
            ? $"Ban Giám Hiệu đã phê duyệt đơn xin nghỉ {durationDays} ngày của con bạn ({req.Student?.FullName}). {mealNote}"
            : $"Giáo viên đã phê duyệt đơn xin nghỉ học của con bạn ({req.Student?.FullName}). {mealNote}";

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
}
