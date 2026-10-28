using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class GuardianService : IGuardianService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public GuardianService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<ApiResponse<ChildLinkRequestResultDto>> SubmitChildLinkRequestAsync(
        int parentUserId,
        SubmitChildLinkDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StudentCode))
        {
            return ApiResponse<ChildLinkRequestResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Mã học sinh là bắt buộc." });
        }

        var parentUser = await _unitOfWork.Repository<User>().GetByIdAsync(parentUserId, cancellationToken);
        if (parentUser == null)
        {
            return ApiResponse<ChildLinkRequestResultDto>.Fail(SystemMessages.MSG07);
        }

        // Ensure parent has a Guardian profile
        var guardian = (await _unitOfWork.Repository<Guardian>().FindAsync(g => g.UserId == parentUserId, cancellationToken)).FirstOrDefault();
        if (guardian == null)
        {
            guardian = new Guardian
            {
                UserId = parentUserId,
                IdentityCardNumber = request.GuardianIdentityCard?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<Guardian>().AddAsync(guardian, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Look up Student
        var normalizedCode = request.StudentCode.Trim().ToUpperInvariant();
        var student = await _unitOfWork.Students.GetByStudentCodeAsync(normalizedCode, cancellationToken);
        if (student == null)
        {
            return ApiResponse<ChildLinkRequestResultDto>.Fail(SystemMessages.MSG11);
        }

        // Check if already linked
        var existingLink = (await _unitOfWork.Repository<StudentGuardian>()
            .FindAsync(sg => sg.StudentId == student.Id && sg.GuardianId == guardian.Id, cancellationToken)).FirstOrDefault();

        if (existingLink != null)
        {
            return ApiResponse<ChildLinkRequestResultDto>.Ok(new ChildLinkRequestResultDto
            {
                RequestId = 0,
                RequestNumber = "ALREADY_LINKED",
                IsAutoVerified = true,
                Status = "Approved",
                Message = $"Tài khoản của bạn đã được liên kết với học sinh {student.FullName}.",
                StudentName = student.FullName,
                StudentCode = student.StudentCode
            }, SystemMessages.MSG13);
        }

        // Generate Request Number: REQ-LNK-YYYYMMDD-XXXX
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var randSuffix = new Random().Next(1000, 9999);
        var requestNumber = $"REQ-LNK-{timestamp}-{randSuffix}";

        // Workflow: Guardian Verification Passed? (Automated Matching Check)
        bool dobMatches = (student.DateOfBirth.Date == request.StudentDateOfBirth.Date);

        var preRegisteredPhoneNumbers = student.StudentGuardians
            .Select(sg => sg.Guardian.User?.PhoneNumber?.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var preRegisteredIdCards = student.StudentGuardians
            .Select(sg => sg.Guardian.IdentityCardNumber?.Trim())
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parentPhone = (parentUser.PhoneNumber ?? request.GuardianPhone).Trim();
        var parentIdCard = (request.GuardianIdentityCard ?? guardian.IdentityCardNumber).Trim();

        bool phoneOrIdCardMatches = false;
        if (!string.IsNullOrEmpty(parentPhone) && preRegisteredPhoneNumbers.Contains(parentPhone))
        {
            phoneOrIdCardMatches = true;
        }
        else if (!string.IsNullOrEmpty(parentIdCard) && preRegisteredIdCards.Contains(parentIdCard))
        {
            phoneOrIdCardMatches = true;
        }

        bool autoVerified = dobMatches && phoneOrIdCardMatches;

        var adminRequest = new AdministrativeRequest
        {
            RequestNumber = requestNumber,
            RequesterUserId = parentUserId,
            StudentId = student.Id,
            RequestType = "ChildLink",
            Status = autoVerified ? RequestStatus.Approved : RequestStatus.UnderReview,
            SubmissionDate = DateTime.UtcNow,
            ProcessedDate = autoVerified ? DateTime.UtcNow : null,
            ReviewerNotes = autoVerified ? "Hệ thống tự động xác thực thành công (Khớp ngày sinh và thông tin liên lạc gốc)." : null
        };

        await _unitOfWork.Repository<AdministrativeRequest>().AddAsync(adminRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var linkDetail = new GuardianLinkDetail
        {
            AdministrativeRequestId = adminRequest.Id,
            StudentCode = student.StudentCode,
            StudentDateOfBirth = request.StudentDateOfBirth,
            Relationship = request.Relationship,
            GuardianPhone = parentPhone,
            GuardianIdentityCard = parentIdCard,
            ProofDocumentUrl = request.ProofDocumentUrl,
            AutoVerificationPassed = autoVerified,
            VerificationDetails = autoVerified
                ? "Tự động xác thực: Khớp ngày sinh và số điện thoại/CCCD."
                : "Cần xác minh thủ công: Ngày sinh hoặc thông tin liên lạc chưa khớp với hồ sơ ban đầu của trường."
        };

        await _unitOfWork.Repository<GuardianLinkDetail>().AddAsync(linkDetail, cancellationToken);

        if (autoVerified)
        {
            // Activate Guardian-Student Linkage (Workflow Step)
            var newLink = new StudentGuardian
            {
                StudentId = student.Id,
                GuardianId = guardian.Id,
                Relationship = request.Relationship,
                IsPrimaryContact = true
            };
            await _unitOfWork.Repository<StudentGuardian>().AddAsync(newLink, cancellationToken);

            // Grant Profile & Class Access to Parent Portal (Send in-system notification)
            var notif = new Notification
            {
                UserId = parentUserId,
                Title = "Xác thực liên kết học sinh thành công",
                Content = $"Hệ thống đã tự động xác thực và kích hoạt liên kết với học sinh {student.FullName} ({student.StudentCode}). Bạn đã có thể truy cập thông tin của con trên Parent Portal.",
                LinkUrl = $"/parent/children/{student.Id}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
        }
        else
        {
            // Notify Parent that request is submitted for manual Office Staff verification
            var notif = new Notification
            {
                UserId = parentUserId,
                Title = "Yêu cầu liên kết học sinh đang chờ duyệt",
                Content = $"Yêu cầu liên kết với học sinh {student.FullName} ({student.StudentCode}) mã số {requestNumber} đã được chuyển tới Phòng Giáo vụ để xác minh thủ công.",
                LinkUrl = "/parent/link-requests",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            parentUserId,
            "SubmitChildLinkRequest",
            nameof(AdministrativeRequest),
            adminRequest.Id.ToString(),
            null,
            $"Request: {requestNumber}, Student: {student.StudentCode}, AutoVerified: {autoVerified}",
            null,
            cancellationToken);

        var resultDto = new ChildLinkRequestResultDto
        {
            RequestId = adminRequest.Id,
            RequestNumber = adminRequest.RequestNumber,
            IsAutoVerified = autoVerified,
            Status = adminRequest.Status.ToString(),
            StudentName = student.FullName,
            StudentCode = student.StudentCode,
            Message = autoVerified
                ? $"Xác thực tự động thành công! Đã kích hoạt liên kết với học sinh {student.FullName}."
                : $"Yêu cầu liên kết mã {requestNumber} đã được ghi nhận và đang chờ Giáo vụ xác minh giấy tờ & danh tính thủ công."
        };

        return ApiResponse<ChildLinkRequestResultDto>.Ok(resultDto, autoVerified ? SystemMessages.MSG13 : SystemMessages.MSG30);
    }

    public async Task<ApiResponse<List<ChildLinkRequestItemDto>>> GetLinkRequestsAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.RequesterUser)
            .Include(ar => ar.Student)
            .Include(ar => ar.ReviewerUser)
            .Include(ar => ar.GuardianLinkDetail)
            .Where(ar => ar.RequestType == "ChildLink");

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(ar => ar.Status == parsedStatus);
        }

        var requests = await query
            .OrderByDescending(ar => ar.SubmissionDate)
            .ToListAsync(cancellationToken);

        var result = requests.Select(ar => new ChildLinkRequestItemDto
        {
            RequestId = ar.Id,
            RequestNumber = ar.RequestNumber,
            SubmissionDate = ar.SubmissionDate,
            Status = ar.Status.ToString(),
            RequesterUserId = ar.RequesterUserId,
            RequesterFullName = ar.RequesterUser.FullName,
            RequesterPhone = ar.RequesterUser.PhoneNumber ?? string.Empty,
            RequesterEmail = ar.RequesterUser.Email,
            StudentId = ar.StudentId,
            StudentCode = ar.GuardianLinkDetail?.StudentCode ?? ar.Student?.StudentCode ?? string.Empty,
            StudentFullName = ar.Student?.FullName,
            StudentDateOfBirth = ar.GuardianLinkDetail?.StudentDateOfBirth ?? ar.Student?.DateOfBirth,
            Relationship = (int)(ar.GuardianLinkDetail?.Relationship ?? RelationshipTypes.Guardian),
            RelationshipName = (ar.GuardianLinkDetail?.Relationship ?? RelationshipTypes.Guardian).ToString(),
            GuardianPhone = ar.GuardianLinkDetail?.GuardianPhone,
            GuardianIdentityCard = ar.GuardianLinkDetail?.GuardianIdentityCard,
            ProofDocumentUrl = ar.GuardianLinkDetail?.ProofDocumentUrl,
            Notes = ar.ReviewerNotes,
            AutoVerificationPassed = ar.GuardianLinkDetail?.AutoVerificationPassed ?? false,
            VerificationDetails = ar.GuardianLinkDetail?.VerificationDetails,
            ReviewerNotes = ar.ReviewerNotes,
            ProcessedDate = ar.ProcessedDate,
            ReviewerFullName = ar.ReviewerUser?.FullName
        }).ToList();

        return ApiResponse<List<ChildLinkRequestItemDto>>.Ok(result);
    }

    public async Task<ApiResponse<bool>> VerifyChildLinkRequestAsync(
        int requestId,
        int reviewerUserId,
        VerifyChildLinkDto review,
        CancellationToken cancellationToken = default)
    {
        var request = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.GuardianLinkDetail)
            .Include(ar => ar.Student)
            .Include(ar => ar.RequesterUser)
            .FirstOrDefaultAsync(ar => ar.Id == requestId && ar.RequestType == "ChildLink", cancellationToken);

        if (request == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy yêu cầu liên kết." });
        }

        if (request.Status != RequestStatus.Submitted && request.Status != RequestStatus.UnderReview)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { $"Yêu cầu đã được xử lý ở trạng thái {request.Status}." });
        }

        request.ReviewerUserId = reviewerUserId;
        request.ProcessedDate = DateTime.UtcNow;
        request.ReviewerNotes = review.ReviewerNotes.Trim();

        if (review.IsApproved)
        {
            request.Status = RequestStatus.Approved;

            // Activate Guardian-Student Linkage in database
            var guardian = (await _unitOfWork.Repository<Guardian>().FindAsync(g => g.UserId == request.RequesterUserId, cancellationToken)).FirstOrDefault();
            if (guardian == null)
            {
                guardian = new Guardian
                {
                    UserId = request.RequesterUserId,
                    IdentityCardNumber = request.GuardianLinkDetail?.GuardianIdentityCard ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Guardian>().AddAsync(guardian, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (request.StudentId.HasValue)
            {
                var existingLink = (await _unitOfWork.Repository<StudentGuardian>()
                    .FindAsync(sg => sg.StudentId == request.StudentId.Value && sg.GuardianId == guardian.Id, cancellationToken)).FirstOrDefault();

                if (existingLink == null)
                {
                    var relType = review.Relationship ?? request.GuardianLinkDetail?.Relationship ?? RelationshipTypes.Guardian;
                    var link = new StudentGuardian
                    {
                        StudentId = request.StudentId.Value,
                        GuardianId = guardian.Id,
                        Relationship = relType,
                        IsPrimaryContact = review.IsPrimaryContact
                    };
                    await _unitOfWork.Repository<StudentGuardian>().AddAsync(link, cancellationToken);
                }
            }

            // Grant Profile & Class Access to Parent Portal (Send in-system notification)
            var notif = new Notification
            {
                UserId = request.RequesterUserId,
                Title = "Yêu cầu liên kết học sinh đã được phê duyệt",
                Content = $"Phòng Giáo vụ đã xác minh hồ sơ thành công. Tài khoản của bạn đã được liên kết với học sinh {request.Student?.FullName}. Bạn có thể vào Parent Portal để theo dõi thông tin lớp học và điểm danh.",
                LinkUrl = $"/parent/children/{request.StudentId}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
        }
        else
        {
            request.Status = RequestStatus.Rejected;

            // Notify Parent of Rejection
            var notif = new Notification
            {
                UserId = request.RequesterUserId,
                Title = "Yêu cầu liên kết học sinh không được chấp thuận",
                Content = $"Yêu cầu liên kết học sinh {request.Student?.FullName} đã bị từ chối. Lý do: {review.ReviewerNotes}",
                LinkUrl = "/parent/link-requests",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
        }

        await _unitOfWork.Repository<AdministrativeRequest>().UpdateAsync(request, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            reviewerUserId,
            review.IsApproved ? "ApproveChildLink" : "RejectChildLink",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Status: UnderReview",
            $"Status: {request.Status}, Notes: {request.ReviewerNotes}",
            null,
            cancellationToken);

        return ApiResponse<bool>.Ok(true, review.IsApproved ? SystemMessages.MSG13 : SystemMessages.MSG32);
    }

    public async Task<ApiResponse<List<LinkedChildSummaryDto>>> GetMyLinkedChildrenAsync(int parentUserId, CancellationToken cancellationToken = default)
    {
        var linkedStudents = await _unitOfWork.Students.GetLinkedStudentsByParentUserIdAsync(parentUserId, cancellationToken);

        var guardian = (await _unitOfWork.Repository<Guardian>().FindAsync(g => g.UserId == parentUserId, cancellationToken)).FirstOrDefault();
        int guardianId = guardian?.Id ?? 0;

        var result = linkedStudents.Select(s =>
        {
            var activeAssignment = s.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
            var myLink = s.StudentGuardians.FirstOrDefault(sg => sg.GuardianId == guardianId);

            return new LinkedChildSummaryDto
            {
                StudentId = s.Id,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                DateOfBirth = s.DateOfBirth,
                Gender = s.Gender,
                AvatarUrl = s.AvatarUrl,
                Status = s.Status,
                CurrentClassId = activeAssignment?.ClassId,
                CurrentClassName = activeAssignment?.Class?.ClassName,
                Grade = activeAssignment?.Class?.Grade,
                IsPlacementPublished = activeAssignment?.Class?.IsPlacementPublished ?? false,
                RelationshipName = myLink?.Relationship.ToString() ?? "Phụ huynh",
                IsPrimaryContact = myLink?.IsPrimaryContact ?? false
            };
        }).ToList();

        return ApiResponse<List<LinkedChildSummaryDto>>.Ok(result);
    }

    public async Task<ApiResponse<ChildDashboardDto>> GetChildDashboardAsync(
        int parentUserId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        // BR-06, BR-34, BR-47: Security check - Verify parent is linked to this child
        var guardian = (await _unitOfWork.Repository<Guardian>().FindAsync(g => g.UserId == parentUserId, cancellationToken)).FirstOrDefault();
        if (guardian == null)
        {
            return ApiResponse<ChildDashboardDto>.Fail(SystemMessages.MSG10);
        }

        var isLinked = await _unitOfWork.Repository<StudentGuardian>()
            .FindAsync(sg => sg.StudentId == studentId && sg.GuardianId == guardian.Id, cancellationToken);

        if (!isLinked.Any())
        {
            return ApiResponse<ChildDashboardDto>.Fail(SystemMessages.MSG10);
        }

        var student = await _unitOfWork.Students.GetStudentWithProfileAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<ChildDashboardDto>.Fail(SystemMessages.MSG11);
        }

        var activeAssignment = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
        var activeClass = activeAssignment?.Class;

        ClassSummaryDto? classInfoDto = null;
        HomeroomTeacherContactDto? teacherContactDto = null;

        if (activeClass != null)
        {
            classInfoDto = new ClassSummaryDto
            {
                Id = activeClass.Id,
                SchoolYearId = activeClass.SchoolYearId,
                SchoolYearName = activeClass.SchoolYear?.YearName ?? string.Empty,
                ClassName = activeClass.ClassName,
                Grade = activeClass.Grade,
                RoomNumber = activeClass.RoomNumber,
                Capacity = activeClass.Capacity,
                Status = activeClass.Status,
                IsPlacementPublished = activeClass.IsPlacementPublished,
                HomeroomTeacherId = activeClass.HomeroomTeacherId,
                HomeroomTeacherName = activeClass.HomeroomTeacher?.User?.FullName,
                HomeroomTeacherStaffCode = activeClass.HomeroomTeacher?.StaffCode
            };

            if (activeClass.HomeroomTeacher?.User != null)
            {
                teacherContactDto = new HomeroomTeacherContactDto
                {
                    TeacherName = activeClass.HomeroomTeacher.User.FullName,
                    PhoneNumber = activeClass.HomeroomTeacher.User.PhoneNumber,
                    Email = activeClass.HomeroomTeacher.User.Email,
                    StaffCode = activeClass.HomeroomTeacher.StaffCode
                };
            }
        }

        // Attendance Mini Summary
        var attendanceRecords = await _unitOfWork.Repository<AttendanceRecord>()
            .FindAsync(ar => ar.StudentId == studentId, cancellationToken);

        int presentCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Present);
        int excusedCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.ExcusedAbsence);
        int unexcusedCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.UnexcusedAbsence);
        int lateCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Late);
        int totalDays = attendanceRecords.Count;
        double rate = totalDays > 0 ? Math.Round((double)(presentCount + lateCount) / totalDays * 100, 1) : 100.0;

        var attendanceSummary = new AttendanceMiniSummaryDto
        {
            TotalTrackedDays = totalDays,
            PresentDays = presentCount,
            ExcusedAbsenceDays = excusedCount,
            UnexcusedAbsenceDays = unexcusedCount,
            LateDays = lateCount,
            AttendanceRatePercentage = rate
        };

        // Today's Lunch Status
        var today = DateTime.UtcNow.Date;
        var lunchReg = await _unitOfWork.Lunch.GetStudentRegistrationForDateAsync(studentId, today, cancellationToken);
        var lunchServiceDay = (await _unitOfWork.Repository<LunchServiceDay>().FindAsync(ls => ls.Date.Date == today, cancellationToken)).FirstOrDefault();

        var lunchStatus = new LunchStatusTodayDto
        {
            IsServiceAvailable = lunchServiceDay != null && lunchServiceDay.IsServing,
            IsRegistered = lunchReg != null && lunchReg.IsActive,
            MealType = lunchReg?.MealType.ToString(),
            CutoffTimeMessage = lunchServiceDay != null ? $"Hạn đăng ký/hủy: {lunchServiceDay.CutoffTime:hh\\:mm}" : "Không có lịch phục vụ"
        };

        var studentProfileDto = new StudentProfileDto
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            DateOfBirth = student.DateOfBirth,
            Gender = student.Gender,
            EnrollmentDate = student.EnrollmentDate,
            Status = student.Status,
            AvatarUrl = student.AvatarUrl,
            CurrentClassId = activeClass?.Id,
            CurrentClassName = activeClass?.ClassName,
            CurrentGrade = activeClass?.Grade,
            CurrentRoomNumber = activeClass?.RoomNumber,
            HomeroomTeacherName = activeClass?.HomeroomTeacher?.User?.FullName,
            CreatedAt = student.CreatedAt,
            Guardians = student.StudentGuardians.Select(sg => new StudentGuardianItemDto
            {
                GuardianId = sg.GuardianId,
                UserId = sg.Guardian.UserId,
                FullName = sg.Guardian.User?.FullName ?? string.Empty,
                PhoneNumber = sg.Guardian.User?.PhoneNumber,
                Email = sg.Guardian.User?.Email,
                IdentityCardNumber = sg.Guardian.IdentityCardNumber,
                Occupation = sg.Guardian.Occupation,
                Address = sg.Guardian.Address,
                Relationship = (int)sg.Relationship,
                RelationshipName = sg.Relationship.ToString(),
                IsPrimaryContact = sg.IsPrimaryContact
            }).ToList()
        };

        var dashboard = new ChildDashboardDto
        {
            Student = studentProfileDto,
            ClassInfo = classInfoDto,
            HomeroomTeacher = teacherContactDto,
            AttendanceSummary = attendanceSummary,
            LunchStatusToday = lunchStatus,
            Guardians = studentProfileDto.Guardians
        };

        return ApiResponse<ChildDashboardDto>.Ok(dashboard);
    }
}
