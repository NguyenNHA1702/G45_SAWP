using Core.Common;
using Core.Configuration;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations;

public class AttendanceService : IAttendanceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISchoolClock _clock;
    private readonly IAuditService _auditService;
    private readonly SchoolOptions _options;

    public AttendanceService(
        IUnitOfWork unitOfWork,
        ISchoolClock clock,
        IAuditService auditService,
        IOptions<SchoolOptions> schoolOptions)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
        _auditService = auditService;
        _options = schoolOptions.Value;
    }

    #region API-1: GetMyClassesAsync (GV lấy danh sách lớp được điểm danh)
    public async Task<ApiResponse<List<TeacherClassOptionDto>>> GetMyClassesAsync(
        int userId, 
        IReadOnlyList<UserRoles> roles,
        DateTime? date, 
        AttendanceSession session,
        CancellationToken ct = default)
    {
        var targetDate = (date ?? _clock.Today).Date;
        var activeYear = await GetActiveSchoolYearAsync(ct);

        // Nếu là Admin, OfficeStaff hoặc SchoolBoard: xem được các lớp Active thuộc năm học hiện tại (ATT-8: không gồm ServiceStaff, ATT-9: lọc theo năm học active)
        if (roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.OfficeStaff) || roles.Contains(UserRoles.SchoolBoard))
        {
            var allClasses = await _unitOfWork.Repository<Class>()
                .FindAsync(c => c.Status == "Active" && (activeYear == null || c.SchoolYearId == activeYear.Id), ct);

            var classIds = allClasses.Select(c => c.Id).ToList();
            var submittedIds = await _unitOfWork.Attendance
                .GetSubmittedClassIdsAsync(classIds, targetDate, session, ct);
            var studentCounts = await GetClassStudentCountsAsync(classIds, targetDate, ct);

            var list = allClasses
                .Select(c => new TeacherClassOptionDto
                {
                    ClassId = c.Id,
                    ClassName = c.ClassName,
                    Grade = c.Grade,
                    SchoolYearName = activeYear?.YearName,
                    StudentCount = studentCounts.GetValueOrDefault(c.Id, 0),
                    AccessReason = "SchoolAdmin",
                    IsSubmitted = submittedIds.Contains(c.Id)
                })
                .OrderBy(c => c.Grade)
                .ThenBy(c => c.ClassName)
                .ToList();

            return ApiResponse<List<TeacherClassOptionDto>>.Ok(list);
        }

        var staffProfiles = await _unitOfWork.Repository<StaffProfile>()
            .FindAsync(sp => sp.UserId == userId, ct);
        var staff = staffProfiles.FirstOrDefault();
        if (staff == null)
        {
            return ApiResponse<List<TeacherClassOptionDto>>.Ok(new List<TeacherClassOptionDto>());
        }

        // Lấy các lớp GV là GVCN thuộc năm học hiện tại
        var homeroomClasses = await _unitOfWork.Repository<Class>()
            .FindAsync(c => c.HomeroomTeacherId == staff.Id && c.Status == "Active" && (activeYear == null || c.SchoolYearId == activeYear.Id), ct);

        var homeroomIds = homeroomClasses.Select(c => c.Id).ToList();
        var homeroomStudentCounts = await GetClassStudentCountsAsync(homeroomIds, targetDate, ct);

        var classDict = new Dictionary<int, TeacherClassOptionDto>();

        foreach (var c in homeroomClasses)
        {
            classDict[c.Id] = new TeacherClassOptionDto
            {
                ClassId = c.Id,
                ClassName = c.ClassName,
                Grade = c.Grade,
                SchoolYearName = activeYear?.YearName,
                StudentCount = homeroomStudentCounts.GetValueOrDefault(c.Id, 0),
                AccessReason = "Homeroom"
            };
        }

        // Check đã nộp điểm danh ngày đó chưa
        var submittedClassIds = await _unitOfWork.Attendance
            .GetSubmittedClassIdsAsync(classDict.Keys, targetDate, session, ct);

        foreach (var option in classDict.Values)
        {
            option.IsSubmitted = submittedClassIds.Contains(option.ClassId);
        }

        var result = classDict.Values
            .OrderBy(c => c.Grade)
            .ThenBy(c => c.ClassName)
            .ToList();

        return ApiResponse<List<TeacherClassOptionDto>>.Ok(result);
    }
    #endregion

    #region API-2: GetAttendanceSheetAsync (Tải bảng điểm danh lớp theo ngày)
    public async Task<ApiResponse<AttendanceSheetDto>> GetAttendanceSheetAsync(
        int classId, 
        DateTime date, 
        AttendanceSession session,
        int userId, 
        IReadOnlyList<UserRoles> roles, 
        CancellationToken ct = default)
    {
        var targetDate = date.Date;

        var cls = await _unitOfWork.Repository<Class>().GetByIdAsync(classId, ct);
        if (cls == null)
        {
            return ApiResponse<AttendanceSheetDto>.Fail(SystemMessages.MSG43);
        }

        if (!await CanViewClassAsync(classId, userId, roles, ct))
        {
            return ApiResponse<AttendanceSheetDto>.Fail(SystemMessages.MSG10);
        }

        var schoolYearId = cls.SchoolYearId > 0 ? cls.SchoolYearId : (int?)null;
        var isSchoolDay = await ValidateSchoolDayAsync(targetDate, schoolYearId, ct);

        var roster = await _unitOfWork.Students.GetActiveRosterAsync(classId, targetDate, ct);
        if (roster.Count == 0)
        {
            // ATT-2: Lớp chưa có học sinh trong ngày -> trả 200 OK kèm danh sách rỗng thay vì 404 gây hiểu nhầm lỗi hệ thống
            return ApiResponse<AttendanceSheetDto>.Ok(new AttendanceSheetDto
            {
                ClassId = classId,
                ClassName = cls.ClassName,
                Date = targetDate,
                Session = session,
                IsSchoolDay = isSchoolDay,
                CanEdit = false,
                LockReason = "EmptyRoster",
                Entries = new List<AttendanceSheetEntryDto>(),
                Summary = new AttendanceSummaryDto()
            });
        }

        var studentIds = roster.Select(s => s.Id).ToList();

        var existingRecords = await LoadExistingRecordsForRosterAsync(classId, studentIds, targetDate, session, track: false, ct);

        var leaves = await _unitOfWork.Attendance
            .GetActiveLeavesCoveringDateAsync(studentIds, targetDate, session, ct);

        var lunches = await _unitOfWork.Lunch
            .GetRegistrationsForStudentsAsync(studentIds, targetDate, track: false, ct);

        // Xác định quyền sửa & lý do khóa (D1 + A7 + mở khóa cả lớp)
        var submittedRecords = existingRecords.Values.Where(r => r.ClassId == classId && IsTeacherSubmittedRecord(r)).ToList();
        var hasTeacherSubmittedAttendance = submittedRecords.Count > 0;
        var (unlockedReq, pendingReq) = await GetActiveReattendanceStateAsync(classId, targetDate, session, ct);
        bool canEdit = false;
        string? lockReason = null;

        // UC-22 (OVR-10): only Office Staff correct or fill in a sheet; the System Administrator only views it
        var isOfficeStaff = roles.Contains(UserRoles.OfficeStaff);

        if (isOfficeStaff)
        {
            if (!isSchoolDay)
            {
                canEdit = false;
                lockReason = "NonSchoolDay";
            }
            else if (targetDate > _clock.Today)
            {
                canEdit = false;
                lockReason = "Future";
            }
            else if (targetDate < _clock.Today && hasTeacherSubmittedAttendance)
            {
                // A7: Ngày quá khứ ĐÃ có điểm danh -> khóa sửa hàng loạt đối với Giáo vụ/Admin; Giáo vụ mở khóa cả lớp để GV chỉnh
                canEdit = false;
                lockReason = "PastDay";
            }
            else if (targetDate == _clock.Today && hasTeacherSubmittedAttendance)
            {
                var (canRecordToday, _) = await CanRecordAsync(classId, targetDate, userId, ct);
                if (canRecordToday)
                {
                    canEdit = true;
                    lockReason = null;
                }
                else
                {
                    // AT-11: today's sheet was submitted by the teacher -> locked for bulk edits by Office Staff not in charge of the class
                    canEdit = false;
                    lockReason = "AlreadySubmitted";
                }
            }
            else
            {
                // Hôm nay (chưa nộp) hoặc ngày quá khứ CHƯA có điểm danh (Giáo vụ bổ sung lần đầu)
                canEdit = true;
                lockReason = null;
            }
        }
        else if (!roles.Contains(UserRoles.Teacher))
        {
            canEdit = false;
            lockReason = "NotAssigned";
        }
        else if (!isSchoolDay)
        {
            canEdit = false;
            lockReason = "NonSchoolDay";
        }
        else if (targetDate > _clock.Today)
        {
            canEdit = false;
            lockReason = "Future";
        }
        else if (targetDate < _clock.Today)
        {
            if (unlockedReq != null)
            {
                var (canRecord, reason) = await CanRecordAsync(classId, targetDate, userId, ct);
                if (!canRecord)
                {
                    canEdit = false;
                    lockReason = reason ?? "NotAssigned";
                }
                else
                {
                    canEdit = true;
                    lockReason = null;
                }
            }
            else
            {
                canEdit = false;
                lockReason = "PastDay";
            }
        }
        else
        {
            if (hasTeacherSubmittedAttendance && unlockedReq == null)
            {
                canEdit = false;
                lockReason = "AlreadySubmitted";
            }
            else
            {
                var (canRecord, reason) = await CanRecordAsync(classId, targetDate, userId, ct);
                if (!canRecord)
                {
                    canEdit = false;
                    lockReason = reason ?? "NotAssigned";
                }
                else
                {
                    canEdit = true;
                }
            }
        }

        // OVR-10 (BR-62): contact notes are shown to school staff with access to the class only
        var contactNotes = CanSeeContactNotes(roles)
            ? await LoadContactNotesAsync(existingRecords.Values.Select(r => r.Id).ToList(), userId, roles, ct)
            : new Dictionary<int, List<AbsenceContactNoteDto>>();

        var entries = new List<AttendanceSheetEntryDto>();
        foreach (var s in roster)
        {
            existingRecords.TryGetValue(s.Id, out var rec);
            leaves.TryGetValue(s.Id, out var leave);
            lunches.TryGetValue(s.Id, out var lunch);

            var isApprovedLeave = leave?.Status == RequestStatus.Approved;
            var suggestedStatus = isApprovedLeave
                ? AttendanceStatus.ExcusedAbsence
                : AttendanceStatus.Present;

            var effectiveStatus = rec?.Status ?? (isApprovedLeave ? AttendanceStatus.ExcusedAbsence : (AttendanceStatus?)null);
            var effectiveRemarks = rec?.Remarks ?? (isApprovedLeave ? $"Vắng có phép theo đơn {leave!.RequestNumber}" : null);

            entries.Add(new AttendanceSheetEntryDto
            {
                StudentId = s.Id,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                AvatarUrl = null,
                RecordId = rec?.Id,
                Status = effectiveStatus,
                SuggestedStatus = suggestedStatus,
                Remarks = effectiveRemarks,
                Leave = leave,
                HasLunch = lunch != null && lunch.IsActive,
                LunchCancelledBy = (lunch != null && !lunch.IsActive) ? lunch.CancellationSource : null,
                ContactNotes = rec != null && contactNotes.TryGetValue(rec.Id, out var notes) ? notes : new List<AbsenceContactNoteDto>()
            });
        }

        var summary = BuildSummary(entries);

        DateTime? submittedAt = submittedRecords.Count > 0 ? submittedRecords.Min(r => r.CreatedAt) : null;
        DateTime? lastUpdatedAt = existingRecords.Count > 0 ? existingRecords.Values.Max(r => r.UpdatedAt ?? r.CreatedAt) : null;
        string? submittedByName = null;

        if (submittedRecords.Count > 0)
        {
            var earliest = submittedRecords.OrderBy(r => r.CreatedAt).First();
            var userRepo = _unitOfWork.Repository<User>();
            var user = userRepo != null ? await userRepo.GetByIdAsync(earliest.RecordedByUserId, ct) : null;
            submittedByName = user?.FullName;
        }

        string? unlockedByName = null;
        if (unlockedReq != null)
        {
            if (unlockedReq.ReviewedByUser != null)
            {
                unlockedByName = unlockedReq.ReviewedByUser.FullName;
            }
            else if (unlockedReq.ReviewedByUserId.HasValue)
            {
                var userRepo = _unitOfWork.Repository<User>();
                var reviewer = userRepo != null ? await userRepo.GetByIdAsync(unlockedReq.ReviewedByUserId.Value, ct) : null;
                unlockedByName = reviewer?.FullName;
            }
        }

        var sheetDto = new AttendanceSheetDto
        {
            ClassId = classId,
            ClassName = cls.ClassName,
            Date = targetDate,
            Session = session,
            IsSchoolDay = isSchoolDay,
            CanEdit = canEdit,
            LockReason = lockReason,
            SubmittedAt = UtcDateTimeHelper.Normalize(submittedAt),
            SubmittedByName = submittedByName,
            LastUpdatedAt = UtcDateTimeHelper.Normalize(lastUpdatedAt),
            IsReattendanceUnlocked = unlockedReq != null,
            UnlockNote = unlockedReq?.ReviewNote ?? unlockedReq?.Reason,
            UnlockedByName = unlockedByName,
            UnlockedAt = UtcDateTimeHelper.Normalize(unlockedReq?.UnlockedAt),
            HasPendingReattendanceRequest = pendingReq != null,
            PendingReattendanceReason = pendingReq?.Reason,
            PendingReattendanceRequestedAt = UtcDateTimeHelper.Normalize(pendingReq?.CreatedAt),
            Entries = entries,
            Summary = summary
        };

        return ApiResponse<AttendanceSheetDto>.Ok(sheetDto);
    }
    #endregion

    #region API-3: RecordDailyAttendanceAsync (Lưu / nộp điểm danh cả lớp)
    public async Task<ApiResponse<AttendanceSubmitResultDto>> RecordDailyAttendanceAsync(
        int classId, 
        SubmitAttendanceSheetDto dto, 
        int userId, 
        IReadOnlyList<UserRoles> roles,
        CancellationToken ct = default)
    {
        // ATT-11: Body rỗng / thiếu ngày (default 0001-01-01) -> trả MSG01 thay vì MSG19
        if (dto.Date == default)
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Ngày điểm danh là bắt buộc." });
        }

        var targetDate = dto.Date.Date;
        var session = dto.Session;
        var now = _clock.Now;
        // UC-22 (OVR-10): only Office Staff (not the System Administrator) fill in a past day or edit any class
        var isOfficeStaff = roles.Contains(UserRoles.OfficeStaff);
        var (activeUnlock, _) = await GetActiveReattendanceStateAsync(classId, targetDate, session, ct, track: true);

        // V1: Validate ngày (OfficeStaff bổ sung ngày quá khứ chưa điểm danh, hoặc GV được Giáo vụ mở khóa cả lớp)
        if (targetDate < _clock.Today)
        {
            if (!isOfficeStaff)
            {
                if (activeUnlock == null)
                {
                    return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG19);
                }
            }
            else if (activeUnlock == null)
            {
                var supplementReason = dto.CorrectionReason?.Trim();
                if (string.IsNullOrEmpty(supplementReason) || supplementReason.Length < 5 || supplementReason.Length > 500)
                {
                    return ApiResponse<AttendanceSubmitResultDto>.Fail(
                        SystemMessages.MSG01,
                        new List<string> { "Bổ sung điểm danh ngày quá khứ bắt buộc nhập lý do từ 5 đến 500 ký tự." });
                }
            }
        }
        if (targetDate > _clock.Today)
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Không thể điểm danh cho ngày tương lai." });
        }

        if (!Enum.IsDefined(typeof(AttendanceSession), session))
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Buổi điểm danh không hợp lệ." });
        }

        // ATT-11: Kiểm tra lớp tồn tại trước khi kiểm tra danh sách học sinh (trả 404 MSG43 thay vì "không thuộc danh sách lớp")
        var cls = await _unitOfWork.Repository<Class>().GetByIdAsync(classId, ct);
        if (cls == null)
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG43);
        }

        // V2: Validate ngày học theo năm học của lớp (ATT-7)
        var schoolYearId = cls.SchoolYearId > 0 ? cls.SchoolYearId : (int?)null;
        if (!await ValidateSchoolDayAsync(targetDate, schoolYearId, ct))
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Ngày đã chọn không phải ngày học." });
        }

        // V3: validate the right to record (Office Staff may record for every class)
        if (!isOfficeStaff)
        {
            var (canRecord, _) = await CanRecordAsync(classId, targetDate, userId, ct);
            if (!canRecord)
            {
                return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG20);
            }
        }

        // V4: Validate danh sách học sinh
        var roster = await _unitOfWork.Students.GetActiveRosterAsync(classId, targetDate, ct);
        var rosterMap = roster.ToDictionary(s => s.Id);
        var rosterIds = roster.Select(s => s.Id).ToHashSet();

        // V5: Validate Entries
        if (dto.Entries == null || dto.Entries.Count == 0)
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Danh sách điểm danh không được để trống." });
        }

        if (dto.Entries.GroupBy(e => e.StudentId).Any(g => g.Count() > 1))
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Danh sách học sinh có bản ghi trùng lặp." });
        }

        var entryIds = dto.Entries.Select(e => e.StudentId).ToHashSet();
        if (!entryIds.SetEquals(rosterIds))
        {
            var missingCount = rosterIds.Except(entryIds).Count();
            if (missingCount > 0)
            {
                return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { $"Còn {missingCount} học sinh chưa được điểm danh." });
            }
            var extraCount = entryIds.Except(rosterIds).Count();
            if (extraCount > 0)
            {
                return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { $"Có {extraCount} học sinh không thuộc danh sách lớp." });
            }
        }

        foreach (var entry in dto.Entries)
        {
            if (!Enum.IsDefined(typeof(AttendanceStatus), entry.Status))
            {
                return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Trạng thái điểm danh không hợp lệ." });
            }
            if (entry.Remarks?.Length > 500)
            {
                return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Ghi chú không được vượt quá 500 ký tự." });
            }
        }

        // Tải bản ghi hiện có theo lớp + học sinh trong danh sách lớp (bao gồm HS mới chuyển lớp trong ngày để tránh đụng unique index StudentId+Date+Session)
        var existingRecords = await LoadExistingRecordsForRosterAsync(classId, rosterIds, targetDate, session, track: true, ct);

        // A7 & BR-38 (BUG-ATT-01): Khi bảng điểm danh đã được giáo viên nộp (hôm nay hoặc quá khứ) -> khóa sửa hàng loạt trừ khi Giáo vụ đã mở khóa cả lớp cho GV
        var hasSubmittedRecords = existingRecords.Values.Any(r => r.ClassId == classId && IsTeacherSubmittedRecord(r));
        if (targetDate < _clock.Today && hasSubmittedRecords && (activeUnlock == null || isOfficeStaff))
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(
                SystemMessages.MSG19,
                new List<string> { "Bảng điểm danh ngày quá khứ đã có dữ liệu và đã khóa sửa hàng loạt. Vui lòng mở khóa bảng điểm danh cho giáo viên để chỉnh sửa." });
        }

        if (targetDate == _clock.Today && hasSubmittedRecords)
        {
            if (isOfficeStaff)
            {
                // AT-11: today's sheet was submitted by the teacher -> Office Staff who are not in charge of the class may not overwrite it in bulk
                var (canRecordToday, _) = await CanRecordAsync(classId, targetDate, userId, ct);
                if (!canRecordToday)
                {
                    return ApiResponse<AttendanceSubmitResultDto>.Fail(
                        SystemMessages.MSG20,
                        new List<string> { "Bảng điểm danh hôm nay đã được giáo viên nộp. Vui lòng dùng chức năng điều chỉnh từng bản ghi kèm lý do." });
                }
            }
            else if (activeUnlock == null)
            {
                return ApiResponse<AttendanceSubmitResultDto>.Fail(
                    SystemMessages.MSG19,
                    new List<string> { "Bảng điểm danh hôm nay đã được nộp và đã khóa chỉnh sửa. Vui lòng gửi yêu cầu mở khóa tới Phòng Giáo vụ nếu cần cập nhật lại." });
            }
        }

        // V6: ExcusedAbsence nếu có đơn Approved trên hệ thống thì hợp lệ; nếu xin phép ngoài hệ thống (chưa có đơn duyệt) thì bắt buộc có ghi chú >= 5 ký tự
        // AT-04: Không cho phép đánh dấu Vắng không phép nếu học sinh đã có đơn nghỉ được duyệt
        var leaves = await _unitOfWork.Attendance.GetActiveLeavesCoveringDateAsync(rosterIds, targetDate, session, ct);
        foreach (var entry in dto.Entries)
        {
            if (entry.Status == AttendanceStatus.ExcusedAbsence)
            {
                var wasAlreadyExcused = existingRecords.TryGetValue(entry.StudentId, out var prevRec)
                    && prevRec.Status == AttendanceStatus.ExcusedAbsence;
                var hasApprovedLeave = leaves.TryGetValue(entry.StudentId, out var leave) && leave.Status == RequestStatus.Approved;
                var hasValidExternalRemarks = !string.IsNullOrWhiteSpace(entry.Remarks) && entry.Remarks.Trim().Length >= 5;
                if (!wasAlreadyExcused && !hasApprovedLeave && !hasValidExternalRemarks)
                {
                    var studentCode = rosterMap.TryGetValue(entry.StudentId, out var s) ? s.StudentCode : entry.StudentId.ToString();
                    return ApiResponse<AttendanceSubmitResultDto>.Fail(
                        SystemMessages.MSG01,
                        new List<string> { $"Học sinh {studentCode}: vắng có phép chưa có đơn duyệt trên hệ thống cần nhập ghi chú tối thiểu 5 ký tự (ví dụ: Xin phép ngoài hệ thống)." });
                }
            }
            else if (entry.Status == AttendanceStatus.UnexcusedAbsence
                && leaves.TryGetValue(entry.StudentId, out var approvedLeave)
                && approvedLeave.Status == RequestStatus.Approved)
            {
                var studentCode = rosterMap.TryGetValue(entry.StudentId, out var s) ? s.StudentCode : entry.StudentId.ToString();
                return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { $"Học sinh {studentCode}: đã có đơn xin nghỉ được duyệt, không thể đánh dấu Vắng không phép." });
            }
        }

        var changes = new List<(int StudentId, AttendanceStatus? OldStatus, AttendanceStatus NewStatus, bool RemarksChanged)>();
        foreach (var entry in dto.Entries)
        {
            existingRecords.TryGetValue(entry.StudentId, out var existing);
            var oldStatus = existing?.Status;
            var remarksChanged = existing != null && (existing.Remarks ?? string.Empty) != (entry.Remarks ?? string.Empty);

            if (existing == null || oldStatus != entry.Status || remarksChanged)
            {
                changes.Add((entry.StudentId, oldStatus, entry.Status, remarksChanged));
            }
        }

        var newlyExcused = changes.Where(c => c.NewStatus == AttendanceStatus.ExcusedAbsence && c.OldStatus != AttendanceStatus.ExcusedAbsence).Select(c => c.StudentId).ToList();
        var leftExcused = changes.Where(c => c.OldStatus == AttendanceStatus.ExcusedAbsence && (c.NewStatus == AttendanceStatus.Present || c.NewStatus == AttendanceStatus.Late)).Select(c => c.StudentId).ToList();

        var result = new AttendanceSubmitResultDto();

        // TRANSACTION GHI DB
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var utcNow = DateTime.UtcNow;

            // T1: Upsert AttendanceRecord (ATT-5: chỉ cập nhật UpdatedAt/UpdatedByUserId khi bản ghi có thay đổi thực sự hoặc xác nhận lần đầu bản ghi sinh từ đơn nghỉ)
            foreach (var entry in dto.Entries)
            {
                if (existingRecords.TryGetValue(entry.StudentId, out var rec))
                {
                    int? leaveRequestId = entry.Status == AttendanceStatus.ExcusedAbsence
                        ? (leaves.TryGetValue(entry.StudentId, out var l) ? l.LeaveRequestId : rec.LeaveRequestId)
                        : null;

                    var hasChanged = rec.ClassId != classId
                        || rec.Status != entry.Status
                        || (rec.Remarks ?? string.Empty) != (entry.Remarks ?? string.Empty)
                        || rec.LeaveRequestId != leaveRequestId;
                    var needsTeacherConfirmation = !IsTeacherSubmittedRecord(rec);

                    if (hasChanged || needsTeacherConfirmation)
                    {
                        rec.ClassId = classId;
                        rec.Status = entry.Status;
                        rec.Remarks = entry.Remarks;
                        rec.LeaveRequestId = leaveRequestId;
                        rec.UpdatedAt = utcNow;
                        rec.UpdatedByUserId = userId;
                    }
                }
                else
                {
                    int? leaveRequestId = (entry.Status == AttendanceStatus.ExcusedAbsence && leaves.TryGetValue(entry.StudentId, out var l))
                        ? l.LeaveRequestId : null;

                    var newRecord = new AttendanceRecord
                    {
                        StudentId = entry.StudentId,
                        ClassId = classId,
                        Date = targetDate,
                        Session = session,
                        Status = entry.Status,
                        Remarks = entry.Remarks,
                        RecordedByUserId = userId,
                        CreatedAt = utcNow,
                        // Đánh dấu bản ghi do GV nộp (kể cả khi giữ nguyên Vắng có phép) để tách biệt với bản ghi tự sinh từ đơn nghỉ (A3)
                        UpdatedAt = leaveRequestId.HasValue ? utcNow : null,
                        UpdatedByUserId = leaveRequestId.HasValue ? userId : null,
                        LeaveRequestId = leaveRequestId
                    };
                    await _unitOfWork.Attendance.AddAsync(newRecord, ct);
                }
            }

            // T2: Suất ăn bán trú (LunchAdjust - Chỉ hủy khi VẮNG CÓ PHÉP; Vắng không phép bếp vẫn nấu ăn bình thường)
            // Suất ăn trưa chỉ phụ thuộc điểm danh buổi Sáng
            // Khôi phục: HS chuyển từ vắng có phép -> đi học/đi muộn, hoặc HS có đơn nghỉ (đã hủy suất) nhưng thực tế đi học
            // (chỉ xét HS có thay đổi trong lần lưu này)
            var attendingIds = changes
                .Where(c => c.NewStatus == AttendanceStatus.Present || c.NewStatus == AttendanceStatus.Late)
                .Select(c => c.StudentId)
                .ToList();
            var relevantStudentIds = session == AttendanceSession.Morning
                ? newlyExcused.Concat(attendingIds).Distinct().ToList()
                : new List<int>();
            if (relevantStudentIds.Count > 0)
            {
                var lunchRegs = await _unitOfWork.Lunch.GetRegistrationsForStudentsAsync(relevantStudentIds, targetDate, track: true, ct);
                var serviceDay = (await _unitOfWork.Repository<LunchServiceDay>().FindAsync(d => d.Date == targetDate, ct)).FirstOrDefault();
                var cutoff = (serviceDay != null && serviceDay.CutoffTime != TimeSpan.Zero) ? serviceDay.CutoffTime : _options.LunchCutoff;
                var isMealLocked = serviceDay?.IsLocked ?? false;

                // Hủy suất ăn CHỈ cho HS mới chuyển sang vắng có phép (nếu trước giờ chốt và chưa khóa sổ bếp)
                foreach (var id in newlyExcused)
                {
                    if (lunchRegs.TryGetValue(id, out var r) && r.IsActive)
                    {
                        if (!isMealLocked && now < targetDate.Add(cutoff))
                        {
                            r.IsActive = false;
                            r.CancellationSource = LunchCancellationSource.Attendance;
                            r.CancelledAt = utcNow;
                            r.CancelledByUserId = userId;
                            r.CancellationReason = $"Vắng (có phép) – điểm danh {targetDate:dd/MM}";
                            result.LunchDeductedCount++;
                        }
                        else
                        {
                            var studentName = rosterMap.TryGetValue(id, out var st) ? st.FullName : $"HS #{id}";
                            result.Warnings.Add(isMealLocked
                                ? $"{studentName}: Sổ bếp đã khóa, không tự động hủy suất ăn."
                                : $"{studentName}: Bếp đã chốt suất lúc {cutoff:hh\\:mm}, không tự động hủy suất ăn.");
                        }
                    }
                }

                // Khôi phục suất ăn CHỈ cho HS chuyển từ vắng có phép -> đi học/đi muộn;
                // HS có đơn nghỉ đã hủy suất nhưng thực tế đi học -> chỉ cảnh báo MSG75 (không tự bật lại suất ăn)
                foreach (var id in attendingIds)
                {
                    if (!lunchRegs.TryGetValue(id, out var r) || r.IsActive) continue;

                    var cancelledByLeftExcused = leftExcused.Contains(id) && r.CancellationSource == LunchCancellationSource.Attendance;
                    var cancelledByLeave = r.CancellationSource == LunchCancellationSource.Leave;
                    if (!cancelledByLeftExcused && !cancelledByLeave) continue;

                    var studentName = rosterMap.TryGetValue(id, out var st) ? st.FullName : $"HS #{id}";
                    if (cancelledByLeave)
                    {
                        result.LunchRestoreSkippedCount++;
                        result.Warnings.Add($"{studentName}: có đơn nghỉ đã hủy suất ăn nhưng thực tế đi học.");
                        continue;
                    }

                    if (!isMealLocked && now < targetDate.Add(cutoff))
                    {
                        r.IsActive = true;
                        r.CancellationSource = null;
                        r.CancelledAt = null;
                        r.CancelledByUserId = null;
                        r.CancellationReason = null;
                        result.LunchRestoredCount++;
                    }
                    else
                    {
                        result.LunchRestoreSkippedCount++;
                        result.Warnings.Add($"{studentName}: Bếp đã chốt suất lúc {cutoff:hh\\:mm}, không khôi phục được suất ăn.");
                    }
                }
            }

            // OVR-10 (BR-62): recording attendance never notifies Parents / Guardians; the homeroom
            // Teacher records a contact note on each unexcused absence instead (AddContactNoteAsync).

            // T3: Đóng yêu cầu mở khóa điểm danh lại sau khi Giáo viên hoàn tất lưu bảng điểm danh
            if (activeUnlock != null && !isOfficeStaff)
            {
                activeUnlock.Status = ReattendanceRequestStatus.Completed;
                activeUnlock.CompletedAt = utcNow;

                if (activeUnlock.ReviewedByUserId.HasValue && activeUnlock.ReviewedByUserId.Value != userId)
                {
                    var notifRepo = _unitOfWork.Repository<Notification>();
                    var userRepo = _unitOfWork.Repository<User>();
                    if (notifRepo != null)
                    {
                        var teacherUser = userRepo != null ? await userRepo.GetByIdAsync(userId, ct) : null;
                        var sessionLabel = session == AttendanceSession.Morning ? "Buổi sáng" : "Buổi chiều";
                        await notifRepo.AddAsync(new Notification
                        {
                            UserId = activeUnlock.ReviewedByUserId.Value,
                            Type = "AttendanceReattendanceCompleted",
                            Title = $"GV đã hoàn tất điểm danh lại lớp {cls.ClassName}",
                            Content = $"{teacherUser?.FullName ?? "Giáo viên"} đã cập nhật xong bảng điểm danh {sessionLabel} ngày {targetDate:dd/MM/yyyy} lớp {cls.ClassName}.",
                            LinkUrl = $"/teacher/attendance?classId={classId}&date={targetDate:yyyy-MM-dd}&session={session}",
                            ReferenceKey = $"reattendance-done:{activeUnlock.Id}:{utcNow.Ticks}",
                            CreatedAt = utcNow,
                            IsRead = false
                        }, ct);
                    }
                }
            }

            // T4: Lưu thay đổi
            await _unitOfWork.SaveChangesAsync(ct);

            // T5: Audit log
            if (changes.Count > 0)
            {
                var action = existingRecords.Count == 0 ? AuditActions.AttendanceSubmit : AuditActions.AttendanceUpdate;
                var oldValues = changes.Select(c => new { c.StudentId, Status = c.OldStatus?.ToString() }).ToList();
                var newValues = changes.Select(c => new { c.StudentId, Status = c.NewStatus.ToString() }).ToList();
                // TODO: pass the client IP address; it is not recorded yet.
                await _auditService.LogActionAsync(userId, action, nameof(AttendanceRecord), $"{classId}:{targetDate:yyyy-MM-dd}:{session}", oldValues, newValues, null, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            await _unitOfWork.CommitTransactionAsync(ct);
        }
        catch (DbUpdateException)
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG09);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }

        result.Summary = BuildSummary(dto.Entries.Select(e => new AttendanceSheetEntryDto { Status = e.Status }));
        return ApiResponse<AttendanceSubmitResultDto>.Ok(result, SystemMessages.MSG18);
    }
    #endregion

    #region API-4: CorrectAttendanceAsync (Giáo vụ sửa điểm danh kèm lý do)
    public async Task<ApiResponse<AttendanceSheetEntryDto>> CorrectAttendanceAsync(
        int recordId, 
        CorrectAttendanceDto dto, 
        int userId, 
        CancellationToken ct = default)
    {
        var rec = await _unitOfWork.Attendance.GetByIdWithStudentClassAsync(recordId, ct);
        if (rec == null)
        {
            return ApiResponse<AttendanceSheetEntryDto>.Fail(SystemMessages.MSG43);
        }

        var reason = dto.CorrectionReason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < 5 || reason.Length > 500)
        {
            return ApiResponse<AttendanceSheetEntryDto>.Fail(SystemMessages.MSG01, new List<string> { "Lý do điều chỉnh bắt buộc từ 5 đến 500 ký tự." });
        }

        if (!Enum.IsDefined(typeof(AttendanceStatus), dto.Status))
        {
            return ApiResponse<AttendanceSheetEntryDto>.Fail(SystemMessages.MSG01, new List<string> { "Trạng thái điểm danh không hợp lệ." });
        }

        if (dto.Status == AttendanceStatus.ExcusedAbsence)
        {
            if (rec.Date.Date < _clock.Today && rec.Status != AttendanceStatus.ExcusedAbsence)
            {
                return ApiResponse<AttendanceSheetEntryDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Điều chỉnh điểm danh ngày quá khứ chỉ dùng để sửa lỗi nhập liệu, không cho phép chuyển sang Vắng có phép." });
            }

            var leaves = await _unitOfWork.Attendance.GetActiveLeavesCoveringDateAsync(new[] { rec.StudentId }, rec.Date, rec.Session, ct);
            var hasApprovedLeave = leaves.TryGetValue(rec.StudentId, out var leave) && leave.Status == RequestStatus.Approved;
            var hasValidExternalRemarks = !string.IsNullOrWhiteSpace(dto.Remarks) && dto.Remarks.Trim().Length >= 5;
            if (!hasApprovedLeave && !hasValidExternalRemarks)
            {
                return ApiResponse<AttendanceSheetEntryDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Vắng có phép chưa có đơn duyệt trên hệ thống cần nhập ghi chú tối thiểu 5 ký tự (ví dụ: Xin phép ngoài hệ thống)." });
            }
        }
        else if (dto.Status == AttendanceStatus.UnexcusedAbsence)
        {
            var leaves = await _unitOfWork.Attendance.GetActiveLeavesCoveringDateAsync(new[] { rec.StudentId }, rec.Date, rec.Session, ct);
            if (leaves != null && leaves.TryGetValue(rec.StudentId, out var leave) && leave.Status == RequestStatus.Approved)
            {
                return ApiResponse<AttendanceSheetEntryDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Học sinh đã có đơn xin nghỉ được duyệt, không thể đánh dấu Vắng không phép." });
            }
        }

        if (dto.Status == rec.Status && (dto.Remarks ?? string.Empty) == (rec.Remarks ?? string.Empty))
        {
            return ApiResponse<AttendanceSheetEntryDto>.Fail(SystemMessages.MSG09, new List<string> { "Không có thay đổi nào để cập nhật." });
        }

        var oldStatus = rec.Status;
        var oldRemarks = rec.Remarks;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var utcNow = DateTime.UtcNow;

            int? newLeaveId = null;
            if (dto.Status == AttendanceStatus.ExcusedAbsence)
            {
                var leaves = await _unitOfWork.Attendance.GetActiveLeavesCoveringDateAsync(new[] { rec.StudentId }, rec.Date, rec.Session, ct);
                if (leaves.TryGetValue(rec.StudentId, out var lv)) newLeaveId = lv.LeaveRequestId;
            }

            rec.Status = dto.Status;
            rec.Remarks = dto.Remarks;
            rec.CorrectionReason = reason;
            rec.LeaveRequestId = newLeaveId;
            rec.UpdatedAt = utcNow;
            rec.UpdatedByUserId = userId;

            // Xử lý suất ăn bán trú (Chỉ hủy khi VẮNG CÓ PHÉP; Vắng không phép bếp vẫn nấu ăn bình thường)
            var lunchDayRepo = _unitOfWork.Repository<LunchServiceDay>();
            var correctServiceDay = lunchDayRepo != null
                ? (await lunchDayRepo.FindAsync(d => d.Date == rec.Date, ct)).FirstOrDefault()
                : null;
            var cutoff = (correctServiceDay != null && correctServiceDay.CutoffTime != TimeSpan.Zero) ? correctServiceDay.CutoffTime : _options.LunchCutoff;
            var isMealLocked = correctServiceDay?.IsLocked ?? false;
            var lunchRegs = await _unitOfWork.Lunch.GetRegistrationsForStudentsAsync(new[] { rec.StudentId }, rec.Date, track: true, ct);

            if (rec.Date == _clock.Today && rec.Session == AttendanceSession.Morning && lunchRegs.TryGetValue(rec.StudentId, out var lunch))
            {
                if (dto.Status == AttendanceStatus.ExcusedAbsence)
                {
                    if (lunch.IsActive && !isMealLocked && _clock.Now < rec.Date.Add(cutoff))
                    {
                        lunch.IsActive = false;
                        lunch.CancellationSource = LunchCancellationSource.Staff;
                        lunch.CancelledAt = utcNow;
                        lunch.CancelledByUserId = userId;
                        lunch.CancellationReason = $"Điều chỉnh điểm danh (Vắng có phép): {reason}";
                    }
                }
                else if (oldStatus == AttendanceStatus.ExcusedAbsence && (dto.Status == AttendanceStatus.Present || dto.Status == AttendanceStatus.Late))
                {
                    if (!lunch.IsActive && (lunch.CancellationSource == LunchCancellationSource.Attendance || lunch.CancellationSource == LunchCancellationSource.Staff))
                    {
                        if (!isMealLocked && _clock.Now < rec.Date.Add(cutoff))
                        {
                            lunch.IsActive = true;
                            lunch.CancellationSource = null;
                            lunch.CancelledAt = null;
                            lunch.CancelledByUserId = null;
                            lunch.CancellationReason = null;
                        }
                    }
                }
            }

            // OVR-10 (BR-62): correcting attendance never notifies Parents / Guardians; the contact
            // notes of the record stay as history whatever the new status is.

            await _unitOfWork.SaveChangesAsync(ct);

            var oldValues = new { Status = oldStatus.ToString(), Remarks = oldRemarks };
            var newValues = new { Status = dto.Status.ToString(), Remarks = dto.Remarks, CorrectionReason = reason };
            // TODO: pass the client IP address; it is not recorded yet.
            await _auditService.LogActionAsync(userId, AuditActions.AttendanceCorrect, nameof(AttendanceRecord), rec.Id.ToString(), oldValues, newValues, null, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitTransactionAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }

        var entryDto = new AttendanceSheetEntryDto
        {
            StudentId = rec.StudentId,
            StudentCode = rec.Student?.StudentCode ?? string.Empty,
            FullName = rec.Student?.FullName ?? string.Empty,
            RecordId = rec.Id,
            Status = rec.Status,
            Remarks = rec.Remarks,
            HasLunch = false
        };

        return ApiResponse<AttendanceSheetEntryDto>.Ok(entryDto, SystemMessages.MSG18);
    }
    #endregion

    #region OVR-10: contact notes on unexcused absences (UC-23 A1, A2; BR-62)
    public async Task<ApiResponse<List<AbsenceContactNoteDto>>> GetContactNotesAsync(
        int recordId,
        int userId,
        IReadOnlyList<UserRoles> roles,
        CancellationToken ct = default)
    {
        var rec = await _unitOfWork.Attendance.GetByIdWithStudentClassAsync(recordId, ct);
        if (rec == null)
        {
            return ApiResponse<List<AbsenceContactNoteDto>>.Fail(SystemMessages.MSG43);
        }

        // BR-62: school staff with access to the class only, never Parents / Guardians
        if (!CanSeeContactNotes(roles) || !await CanViewClassAsync(rec.ClassId, userId, roles, ct))
        {
            return ApiResponse<List<AbsenceContactNoteDto>>.Fail(SystemMessages.MSG10);
        }

        var notes = await LoadContactNotesAsync(new[] { rec.Id }, userId, roles, ct);
        return ApiResponse<List<AbsenceContactNoteDto>>.Ok(
            notes.TryGetValue(rec.Id, out var list) ? list : new List<AbsenceContactNoteDto>());
    }

    public async Task<ApiResponse<AbsenceContactNoteDto>> AddContactNoteAsync(
        int recordId,
        CreateAbsenceContactNoteDto dto,
        int userId,
        IReadOnlyList<UserRoles> roles,
        CancellationToken ct = default)
    {
        var rec = await _unitOfWork.Attendance.GetByIdWithStudentClassAsync(recordId, ct);
        if (rec == null)
        {
            return ApiResponse<AbsenceContactNoteDto>.Fail(SystemMessages.MSG43);
        }

        // UC-23 A1: the homeroom Teacher of the class, or Office Staff following the absence up
        if (!roles.Contains(UserRoles.OfficeStaff) && !await IsHomeroomTeacherAsync(rec.ClassId, userId, roles, ct))
        {
            return ApiResponse<AbsenceContactNoteDto>.Fail(SystemMessages.MSG10);
        }

        // UC-23 E2: only a record of status Unexcused absence takes a contact note
        if (rec.Status != AttendanceStatus.UnexcusedAbsence)
        {
            return ApiResponse<AbsenceContactNoteDto>.Fail(
                SystemMessages.MSG09,
                new List<string> { "Chỉ có thể thêm ghi chú liên hệ khi bản ghi điểm danh đang ở trạng thái Vắng không phép." });
        }

        var errors = new List<string>();
        DateTime? contactedAtUtc = null;
        if (dto.ContactedAt == null || dto.ContactedAt.Value == default)
        {
            errors.Add("Vui lòng nhập thời điểm liên hệ.");
        }
        else
        {
            contactedAtUtc = ToUtc(dto.ContactedAt.Value);
            if (contactedAtUtc.Value > DateTime.UtcNow.AddMinutes(5))
            {
                errors.Add("Thời điểm liên hệ không được ở trong tương lai.");
            }
            else if (_clock.ToSchoolTime(contactedAtUtc.Value).Date < rec.Date.Date.AddDays(-7))
            {
                errors.Add("Thời điểm liên hệ không được trước ngày vắng học quá 7 ngày.");
            }
        }
        if (dto.Channel == null || !Enum.IsDefined(typeof(ContactChannel), dto.Channel.Value))
        {
            errors.Add("Vui lòng chọn hình thức liên hệ.");
        }
        if (dto.Outcome == null || !Enum.IsDefined(typeof(ContactOutcome), dto.Outcome.Value))
        {
            errors.Add("Vui lòng chọn kết quả liên hệ.");
        }
        var noteText = dto.Note?.Trim() ?? string.Empty;
        if (noteText.Length == 0)
        {
            errors.Add("Vui lòng nhập nội dung ghi chú.");
        }
        else if (noteText.Length > 500)
        {
            errors.Add("Nội dung ghi chú không được vượt quá 500 ký tự.");
        }
        if (errors.Count > 0)
        {
            return ApiResponse<AbsenceContactNoteDto>.Fail(SystemMessages.MSG01, errors);
        }

        var note = new AbsenceContactNote
        {
            AttendanceRecordId = rec.Id,
            ContactedAt = contactedAtUtc!.Value,
            Channel = dto.Channel!.Value,
            Outcome = dto.Outcome!.Value,
            Note = noteText,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.Repository<AbsenceContactNote>().AddAsync(note, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // TODO: pass the client IP address; it is not recorded yet.
            await _auditService.LogActionAsync(
                userId,
                AuditActions.AttendanceContactNoteCreate,
                nameof(AbsenceContactNote),
                note.Id.ToString(),
                null,
                new
                {
                    AttendanceRecordId = rec.Id,
                    rec.StudentId,
                    Date = rec.Date.ToString("yyyy-MM-dd"),
                    Session = rec.Session.ToString(),
                    ContactedAt = note.ContactedAt,
                    Channel = note.Channel.ToString(),
                    Outcome = note.Outcome.ToString(),
                    note.Note
                },
                null,
                ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitTransactionAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }

        var creator = await _unitOfWork.Repository<User>().GetByIdAsync(userId, ct);
        return ApiResponse<AbsenceContactNoteDto>.Ok(
            ToContactNoteDto(note, creator?.FullName, null, userId, roles),
            SystemMessages.MSG55);
    }

    public async Task<ApiResponse<AbsenceContactNoteDto>> SupersedeContactNoteAsync(
        int noteId,
        SupersedeAbsenceContactNoteDto dto,
        int userId,
        IReadOnlyList<UserRoles> roles,
        CancellationToken ct = default)
    {
        var noteRepo = _unitOfWork.Repository<AbsenceContactNote>();
        var note = await noteRepo.GetByIdAsync(noteId, ct);
        if (note == null)
        {
            return ApiResponse<AbsenceContactNoteDto>.Fail(SystemMessages.MSG43);
        }

        // UC-23 E2: only the note's author or Office Staff, and only once (cannot be undone)
        var isOfficeStaff = roles.Contains(UserRoles.OfficeStaff);
        if (note.SupersededAt != null || (note.CreatedByUserId != userId && !isOfficeStaff))
        {
            return ApiResponse<AbsenceContactNoteDto>.Fail(SystemMessages.MSG09);
        }

        // An author who is not Office Staff must still be the homeroom teacher of the absence's class (BR-07)
        if (!isOfficeStaff)
        {
            var rec = await _unitOfWork.Attendance.GetByIdWithStudentClassAsync(note.AttendanceRecordId, ct);
            if (rec == null || !await IsHomeroomTeacherAsync(rec.ClassId, userId, roles, ct))
            {
                return ApiResponse<AbsenceContactNoteDto>.Fail(SystemMessages.MSG09);
            }
        }

        var reason = dto.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 5 || reason.Length > 500)
        {
            return ApiResponse<AbsenceContactNoteDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Lý do đánh dấu không còn hiệu lực bắt buộc từ 5 đến 500 ký tự." });
        }

        var utcNow = DateTime.UtcNow;
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            note.SupersededAt = utcNow;
            note.SupersededByUserId = userId;
            note.SupersedeReason = reason;
            await noteRepo.UpdateAsync(note, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // TODO: pass the client IP address; it is not recorded yet.
            await _auditService.LogActionAsync(
                userId,
                AuditActions.AttendanceContactNoteSupersede,
                nameof(AbsenceContactNote),
                note.Id.ToString(),
                new { Superseded = false },
                new { Superseded = true, note.AttendanceRecordId, Reason = reason },
                null,
                ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitTransactionAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else marked the note superseded at the same time (SupersededAt is a concurrency token)
            await _unitOfWork.RollbackTransactionAsync(ct);
            return ApiResponse<AbsenceContactNoteDto>.Fail(SystemMessages.MSG09);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }

        var userRepo = _unitOfWork.Repository<User>();
        var creator = await userRepo.GetByIdAsync(note.CreatedByUserId, ct);
        var marker = note.CreatedByUserId == userId ? creator : await userRepo.GetByIdAsync(userId, ct);
        return ApiResponse<AbsenceContactNoteDto>.Ok(
            ToContactNoteDto(note, creator?.FullName, marker?.FullName, userId, roles),
            SystemMessages.MSG319);
    }

    public async Task<ApiResponse<List<AbsenceWithoutContactNoteDto>>> GetAbsencesWithoutContactNoteAsync(
        DateTime from,
        DateTime to,
        int? classId,
        IReadOnlyList<UserRoles> roles,
        CancellationToken ct = default)
    {
        // UC-23 A2: Office Staff follow up the absences that have no contact note in effect
        if (!roles.Contains(UserRoles.OfficeStaff))
        {
            return ApiResponse<List<AbsenceWithoutContactNoteDto>>.Fail(SystemMessages.MSG10);
        }

        var fromDate = from.Date;
        var toDate = to.Date;
        if (toDate < fromDate || (toDate - fromDate).TotalDays > 366)
        {
            return ApiResponse<List<AbsenceWithoutContactNoteDto>>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Khoảng thời gian không hợp lệ (tối đa 366 ngày và Từ ngày phải trước Đến ngày)." });
        }

        var effectiveTo = toDate > _clock.Today ? _clock.Today : toDate;
        if (effectiveTo < fromDate)
        {
            return ApiResponse<List<AbsenceWithoutContactNoteDto>>.Ok(new List<AbsenceWithoutContactNoteDto>());
        }

        var query = _unitOfWork.Repository<AttendanceRecord>().Query()
            .AsNoTracking()
            .Where(r => r.Status == AttendanceStatus.UnexcusedAbsence
                     && r.Date >= fromDate
                     && r.Date <= effectiveTo
                     // a superseded note does not count as a contact note (BR-62)
                     && !r.ContactNotes.Any(n => n.SupersededAt == null));
        if (classId.HasValue)
        {
            query = query.Where(r => r.ClassId == classId.Value);
        }

        var list = await query
            .Select(r => new AbsenceWithoutContactNoteDto
            {
                RecordId = r.Id,
                StudentId = r.StudentId,
                StudentCode = r.Student.StudentCode,
                FullName = r.Student.FullName,
                ClassId = r.ClassId,
                ClassName = r.Class.ClassName,
                Grade = r.Class.Grade,
                HomeroomTeacherName = r.Class.HomeroomTeacher != null ? r.Class.HomeroomTeacher.User.FullName : null,
                Date = r.Date,
                Session = r.Session,
                Remarks = r.Remarks,
                SupersededNoteCount = r.ContactNotes.Count()
            })
            .ToListAsync(ct);

        var ordered = list
            .OrderBy(a => a.Grade)
            .ThenBy(a => a.ClassName)
            .ThenBy(a => a.Date)
            .ThenBy(a => a.Session)
            .ThenBy(a => a.FullName)
            .ToList();
        return ApiResponse<List<AbsenceWithoutContactNoteDto>>.Ok(ordered);
    }
    #endregion

    #region API-5: GetClassSummaryAsync (Báo cáo tổng hợp chuyên cần lớp)
    public async Task<ApiResponse<ClassAttendanceSummaryDto>> GetClassSummaryAsync(
        int classId, 
        DateTime from, 
        DateTime to, 
        int userId, 
        IReadOnlyList<UserRoles> roles, 
        CancellationToken ct = default)
    {
        var fromDate = from.Date;
        var toDate = to.Date;

        if (toDate < fromDate || (toDate - fromDate).TotalDays > 366)
        {
            return ApiResponse<ClassAttendanceSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Khoảng thời gian không hợp lệ (tối đa 366 ngày và Từ ngày phải trước Đến ngày)." });
        }

        if (!await CanViewClassAsync(classId, userId, roles, ct))
        {
            return ApiResponse<ClassAttendanceSummaryDto>.Fail(SystemMessages.MSG10);
        }

        var cls = await _unitOfWork.Repository<Class>().GetByIdAsync(classId, ct);
        if (cls == null)
        {
            return ApiResponse<ClassAttendanceSummaryDto>.Fail(SystemMessages.MSG43);
        }

        // ATT-13: Chỉ tính thống kê chuyên cần cho các ngày đã/đang diễn ra (<= Today), không tính bản ghi sinh sẵn trong tương lai
        var effectiveTo = toDate > _clock.Today ? _clock.Today : toDate;
        var rows = effectiveTo < fromDate
            ? Array.Empty<StudentAttendanceRowDto>()
            : await _unitOfWork.Attendance.GetClassStatusCountsAsync(classId, fromDate, effectiveTo, ct);

        // ATT-11 (UC-23 E1): Không có dữ liệu điểm danh trong khoảng thời gian -> trả 404 MSG43
        var markedTotalCheck = rows.Sum(r => r.Present + r.Late + r.ExcusedAbsence + r.UnexcusedAbsence);
        if (rows.Count == 0 || markedTotalCheck == 0)
        {
            return ApiResponse<ClassAttendanceSummaryDto>.Fail(SystemMessages.MSG43);
        }

        // Số ngày học thực tế có bản ghi (chỉ tính ngày học thực tế theo SchoolCalendarDay của năm học lớp, không tính ngày nghỉ / cuối tuần trừ học bù)
        var schoolYearId = cls.SchoolYearId > 0 ? cls.SchoolYearId : (int?)null;
        var classRecords = await _unitOfWork.Repository<AttendanceRecord>()
            .FindAsync(r => r.ClassId == classId && r.Date >= fromDate && r.Date <= effectiveTo, ct);
        var calMap = await GetCalendarDayMapAsync(fromDate, effectiveTo, schoolYearId, ct);
        var schoolDaysCount = classRecords
            .Select(r => r.Date.Date)
            .Distinct()
            .Count(d => IsSchoolDayByMap(d, calMap));
        var markedTotal = markedTotalCheck;
        var notMarked = rows.Sum(r => r.NotMarked);

        var totals = new AttendanceSummaryDto
        {
            Total = markedTotal,
            Present = rows.Sum(r => r.Present),
            Late = rows.Sum(r => r.Late),
            ExcusedAbsence = rows.Sum(r => r.ExcusedAbsence),
            UnexcusedAbsence = rows.Sum(r => r.UnexcusedAbsence),
            NotMarked = notMarked
        };

        decimal rate = totals.Total == 0 ? 0 : Math.Round((decimal)(totals.Present + totals.Late) * 100m / totals.Total, 2);

        var dto = new ClassAttendanceSummaryDto
        {
            ClassId = classId,
            ClassName = cls.ClassName,
            From = fromDate,
            To = toDate,
            SchoolDays = schoolDaysCount,
            Totals = totals,
            AttendanceRate = rate,
            Students = rows.ToList()
        };

        return ApiResponse<ClassAttendanceSummaryDto>.Ok(dto);
    }

    public async Task<ApiResponse<ClassAttendanceMatrixDto>> GetClassMatrixAsync(
        int classId,
        DateTime from,
        DateTime to,
        int userId,
        IReadOnlyList<UserRoles> roles,
        CancellationToken ct = default)
    {
        var fromDate = from.Date;
        var toDate = to.Date;

        if (toDate < fromDate || (toDate - fromDate).TotalDays >= 31)
        {
            return ApiResponse<ClassAttendanceMatrixDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Khoảng thời gian xem ma trận tối đa 31 ngày và Từ ngày phải trước Đến ngày." });
        }

        if (!await CanViewClassAsync(classId, userId, roles, ct))
        {
            return ApiResponse<ClassAttendanceMatrixDto>.Fail(SystemMessages.MSG10);
        }

        var cls = await _unitOfWork.Repository<Class>().GetByIdAsync(classId, ct);
        if (cls == null)
        {
            return ApiResponse<ClassAttendanceMatrixDto>.Fail(SystemMessages.MSG43);
        }

        var effectiveTo = toDate > _clock.Today ? _clock.Today : toDate;
        var students = effectiveTo < fromDate
            ? new List<ClassMatrixStudentDto>()
            : await _unitOfWork.Attendance.GetClassMatrixAsync(classId, fromDate, effectiveTo, ct);

        if (students.Count == 0 || students.All(s => s.Cells.Count == 0))
        {
            return ApiResponse<ClassAttendanceMatrixDto>.Fail(SystemMessages.MSG43);
        }

        var schoolYearId = cls.SchoolYearId > 0 ? cls.SchoolYearId : (int?)null;
        var calMap = await GetCalendarDayMapAsync(fromDate, effectiveTo, schoolYearId, ct);
        var recordedDates = students.SelectMany(s => s.Cells.Select(c => c.Date.Date)).ToHashSet();

        var dates = new List<DateTime>();
        for (var d = fromDate; d <= effectiveTo; d = d.AddDays(1))
        {
            if (IsSchoolDayByMap(d, calMap) || recordedDates.Contains(d))
            {
                dates.Add(d);
            }
        }

        return ApiResponse<ClassAttendanceMatrixDto>.Ok(new ClassAttendanceMatrixDto
        {
            ClassId = classId,
            ClassName = cls.ClassName,
            From = fromDate,
            To = effectiveTo,
            Dates = dates,
            Students = students
        });
    }
    #endregion

    #region API-6: GetStudentHistoryAsync (Lịch sử điểm danh học sinh)
    public async Task<ApiResponse<StudentAttendanceHistoryDto>> GetStudentHistoryAsync(
        int studentId, 
        DateTime from, 
        DateTime to, 
        int userId, 
        IReadOnlyList<UserRoles> roles, 
        CancellationToken ct = default)
    {
        var fromDate = from.Date;
        var toDate = to.Date;

        if (toDate < fromDate || (toDate - fromDate).TotalDays > 366)
        {
            return ApiResponse<StudentAttendanceHistoryDto>.Fail(SystemMessages.MSG01, new List<string> { "Khoảng thời gian không hợp lệ (tối đa 366 ngày)." });
        }

        // Kiểm tra phân quyền
        if (roles.Contains(UserRoles.Parent))
        {
            var isGuardian = await _unitOfWork.Students.IsGuardianOfAsync(studentId, userId, ct);
            if (!isGuardian)
            {
                return ApiResponse<StudentAttendanceHistoryDto>.Fail(SystemMessages.MSG10);
            }
        }
        else if (roles.Contains(UserRoles.Teacher) 
                 && !roles.Contains(UserRoles.OfficeStaff) 
                 && !roles.Contains(UserRoles.SchoolBoard) 
                 && !roles.Contains(UserRoles.Admin))
        {
            var studentWithClass = await _unitOfWork.Students.GetStudentWithProfileAsync(studentId, ct);
            var currentAssignment = studentWithClass?.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
            if (currentAssignment == null || !await CanViewClassAsync(currentAssignment.ClassId, userId, roles, ct))
            {
                return ApiResponse<StudentAttendanceHistoryDto>.Fail(SystemMessages.MSG10);
            }
        }

        var student = await _unitOfWork.Students.GetStudentWithProfileAsync(studentId, ct);
        if (student == null)
        {
            return ApiResponse<StudentAttendanceHistoryDto>.Fail(SystemMessages.MSG11);
        }

        var activeAssignment = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
        var className = activeAssignment?.Class?.ClassName;

        var records = await _unitOfWork.Attendance
            .GetStudentAttendanceHistoryAsync(studentId, fromDate, toDate, ct);

        bool isParent = roles.Contains(UserRoles.Parent);

        var days = records.Select(r => new AttendanceDayDto
        {
            Date = r.Date,
            Session = r.Session,
            Status = r.Status,
            Remarks = isParent ? null : r.Remarks, // BR-51: không để lộ ghi chú nội bộ cho phụ huynh
            LeaveRequestNumber = r.LeaveRequest?.AdministrativeRequest?.RequestNumber
        }).ToList();

        // ATT-13: Vẫn trả về toàn bộ days (gồm lịch nghỉ đã duyệt tương lai để hiển thị trên lịch),
        // nhưng chỉ tính Summary trên các ngày đã/đang diễn ra (<= Today)
        var realizedDays = days.Where(d => d.Date.Date <= _clock.Today).ToList();
        var summary = new AttendanceSummaryDto
        {
            Total = realizedDays.Count,
            Present = realizedDays.Count(d => d.Status == AttendanceStatus.Present),
            Late = realizedDays.Count(d => d.Status == AttendanceStatus.Late),
            ExcusedAbsence = realizedDays.Count(d => d.Status == AttendanceStatus.ExcusedAbsence),
            UnexcusedAbsence = realizedDays.Count(d => d.Status == AttendanceStatus.UnexcusedAbsence),
            NotMarked = 0
        };

        var historyDto = new StudentAttendanceHistoryDto
        {
            StudentId = studentId,
            FullName = student.FullName,
            ClassName = className,
            From = fromDate,
            To = toDate,
            Days = days,
            Summary = summary
        };

        return ApiResponse<StudentAttendanceHistoryDto>.Ok(historyDto);
    }
    #endregion

    #region Private Helpers (Thuật toán kiểm tra phân quyền & ngày học)

    private static AttendanceSummaryDto BuildSummary(IEnumerable<AttendanceSheetEntryDto> entries)
    {
        var list = entries.ToList();
        return new AttendanceSummaryDto
        {
            Total = list.Count,
            Present = list.Count(e => e.Status == AttendanceStatus.Present),
            Late = list.Count(e => e.Status == AttendanceStatus.Late),
            ExcusedAbsence = list.Count(e => e.Status == AttendanceStatus.ExcusedAbsence),
            UnexcusedAbsence = list.Count(e => e.Status == AttendanceStatus.UnexcusedAbsence),
            NotMarked = list.Count(e => e.Status == null)
        };
    }

    /// <summary>
    /// Kiểm tra GV có quyền điểm danh lớp này trong ngày không (GVCN hoặc GV tiết đầu)
    /// </summary>
    private async Task<(bool canRecord, string? reason)> CanRecordAsync(
        int classId, 
        DateTime date, 
        int userId, 
        CancellationToken ct = default)
    {
        var staffRepo = _unitOfWork.Repository<StaffProfile>();
        if (staffRepo == null) return (false, "NotAssigned");
        var staffList = await staffRepo.FindAsync(sp => sp.UserId == userId, ct);
        var staff = staffList.FirstOrDefault();

        if (staff == null) return (false, "NotAssigned");

        var cls = await _unitOfWork.Repository<Class>()
            .GetByIdAsync(classId, ct);

        if (cls == null || cls.Status != "Active") return (false, "NotAssigned");

        // Là GVCN
        if (cls.HomeroomTeacherId == staff.Id) return (true, null);

        return (false, "NotAssigned");
    }

    /// <summary>
    /// Kiểm tra quyền xem lớp (BGH, Giáo vụ, Admin, hoặc GVCN của lớp — ATT-8: không gồm ServiceStaff)
    /// </summary>
    private async Task<bool> CanViewClassAsync(
        int classId, 
        int userId, 
        IReadOnlyList<UserRoles> roles, 
        CancellationToken ct = default)
    {
        if (roles.Contains(UserRoles.OfficeStaff) || roles.Contains(UserRoles.SchoolBoard) || roles.Contains(UserRoles.Admin))
        {
            return true;
        }

        if (roles.Contains(UserRoles.Teacher))
        {
            var staffList = await _unitOfWork.Repository<StaffProfile>()
                .FindAsync(sp => sp.UserId == userId, ct);
            var staff = staffList.FirstOrDefault();

            if (staff == null) return false;

            var cls = await _unitOfWork.Repository<Class>().GetByIdAsync(classId, ct);
            return cls != null && cls.HomeroomTeacherId == staff.Id;
        }

        return false;
    }

    private static bool IsTeacherSubmittedRecord(AttendanceRecord r) =>
        r.LeaveRequestId == null || r.UpdatedByUserId != null;

    private async Task<SchoolYear?> GetActiveSchoolYearAsync(CancellationToken ct)
    {
        var yearRepo = _unitOfWork.Repository<SchoolYear>();
        if (yearRepo == null) return null;

        var activeYears = await yearRepo.FindAsync(y => y.IsActive, ct);
        return activeYears.OrderByDescending(y => y.StartDate).FirstOrDefault();
    }

    private async Task<Dictionary<int, AttendanceRecord>> LoadExistingRecordsForRosterAsync(
        int classId,
        IReadOnlyCollection<int> studentIds,
        DateTime targetDate,
        AttendanceSession session,
        bool track,
        CancellationToken ct)
    {
        var byClass = await _unitOfWork.Attendance.GetDailyClassAttendanceAsync(classId, targetDate, session, track, ct);
        var map = (byClass ?? Array.Empty<AttendanceRecord>()).ToDictionary(r => r.StudentId);

        var missingIds = studentIds.Where(id => !map.ContainsKey(id)).ToList();
        if (missingIds.Count > 0)
        {
            var byStudents = await _unitOfWork.Attendance.GetDailyStudentAttendanceAsync(missingIds, targetDate, session, track, ct);
            if (byStudents != null)
            {
                foreach (var rec in byStudents)
                {
                    map[rec.StudentId] = rec;
                }
            }
        }

        return map;
    }

    private async Task<Dictionary<int, int>> GetClassStudentCountsAsync(
        IReadOnlyCollection<int> classIds,
        DateTime targetDate,
        CancellationToken ct)
    {
        if (classIds.Count == 0) return new Dictionary<int, int>();

        var assignRepo = _unitOfWork.Repository<ClassAssignment>();
        if (assignRepo == null) return new Dictionary<int, int>();

        var dayStart = targetDate.Date;
        var nextDay = dayStart.AddDays(1);
        var assignments = await assignRepo.FindAsync(
            ca => classIds.Contains(ca.ClassId)
               && ca.IsActive
               && ca.StartDate < nextDay
               && (ca.EndDate == null || ca.EndDate >= dayStart)
               && ca.Student != null
               && ca.Student.Status == "Active",
            ct);

        return assignments
            .GroupBy(ca => ca.ClassId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StudentId).Distinct().Count());
    }

    private async Task<Dictionary<DateTime, bool>> GetCalendarDayMapAsync(
        DateTime fromDate,
        DateTime toDate,
        int? schoolYearId,
        CancellationToken ct)
    {
        var repo = _unitOfWork.Repository<SchoolCalendarDay>();
        if (repo == null) return new Dictionary<DateTime, bool>();

        var targetYearId = schoolYearId ?? (await GetActiveSchoolYearAsync(ct))?.Id;
        var days = targetYearId.HasValue
            ? await repo.FindAsync(d => d.SchoolYearId == targetYearId.Value && d.Date >= fromDate.Date && d.Date <= toDate.Date, ct)
            : await repo.FindAsync(d => d.Date >= fromDate.Date && d.Date <= toDate.Date, ct);

        return days
            .GroupBy(d => d.Date.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().IsSchoolDay);
    }

    private static bool IsSchoolDayByMap(DateTime date, IReadOnlyDictionary<DateTime, bool> calendarMap) =>
        SchoolCalendarHelper.IsSchoolDay(date, calendarMap);

    /// <summary>
    /// Kiểm tra xem một ngày có phải ngày học theo lịch của trường không (lọc theo năm học — ATT-7).
    /// Ưu tiên kiểm tra SchoolCalendarDay trước để hỗ trợ ngày học bù (T7/CN có IsSchoolDay = true) và ngày nghỉ lễ riêng.
    /// </summary>
    private async Task<bool> ValidateSchoolDayAsync(DateTime date, int? schoolYearId = null, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<SchoolCalendarDay>();
        IReadOnlyList<SchoolCalendarDay> calendarDays = Array.Empty<SchoolCalendarDay>();
        if (repo != null)
        {
            var targetYearId = schoolYearId ?? (await GetActiveSchoolYearAsync(ct))?.Id;
            calendarDays = targetYearId.HasValue
                ? await repo.FindAsync(d => d.SchoolYearId == targetYearId.Value && d.Date == date.Date, ct)
                : await repo.FindAsync(d => d.Date == date.Date, ct);
        }

        var calendarDay = calendarDays.OrderByDescending(d => d.Id).FirstOrDefault();

        if (calendarDay != null)
        {
            return calendarDay.IsSchoolDay;
        }

        // Mặc định Thứ 2 đến Thứ 6 trừ ngày lễ cố định là ngày học
        return SchoolCalendarHelper.IsDefaultSchoolDay(date);
    }

    /// <summary>BR-62: contact notes are for school staff (Teacher, Office Staff, School Board), never Parents / Guardians or the System Administrator.</summary>
    private static bool CanSeeContactNotes(IReadOnlyList<UserRoles> roles) =>
        roles.Contains(UserRoles.OfficeStaff) || roles.Contains(UserRoles.SchoolBoard) || roles.Contains(UserRoles.Teacher);

    /// <summary>The user is a Teacher and the homeroom teacher of the class.</summary>
    private async Task<bool> IsHomeroomTeacherAsync(int classId, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct)
    {
        if (!roles.Contains(UserRoles.Teacher)) return false;
        var staff = (await _unitOfWork.Repository<StaffProfile>().FindAsync(sp => sp.UserId == userId, ct)).FirstOrDefault();
        if (staff == null) return false;
        var cls = await _unitOfWork.Repository<Class>().GetByIdAsync(classId, ct);
        return cls != null && cls.HomeroomTeacherId == staff.Id;
    }

    private async Task<Dictionary<int, List<AbsenceContactNoteDto>>> LoadContactNotesAsync(
        IReadOnlyCollection<int> recordIds,
        int userId,
        IReadOnlyList<UserRoles> roles,
        CancellationToken ct)
    {
        var result = new Dictionary<int, List<AbsenceContactNoteDto>>();
        var repo = _unitOfWork.Repository<AbsenceContactNote>();
        if (repo == null || recordIds.Count == 0) return result;

        var rows = await repo.Query()
            .AsNoTracking()
            .Where(n => recordIds.Contains(n.AttendanceRecordId))
            .Select(n => new
            {
                Note = n,
                CreatedByName = n.CreatedByUser != null ? n.CreatedByUser.FullName : null,
                SupersededByName = n.SupersededByUser != null ? n.SupersededByUser.FullName : null
            })
            .ToListAsync(ct);

        foreach (var row in rows.OrderByDescending(r => r.Note.CreatedAt).ThenByDescending(r => r.Note.Id))
        {
            if (!result.TryGetValue(row.Note.AttendanceRecordId, out var list))
            {
                list = new List<AbsenceContactNoteDto>();
                result[row.Note.AttendanceRecordId] = list;
            }
            list.Add(ToContactNoteDto(row.Note, row.CreatedByName, row.SupersededByName, userId, roles));
        }
        return result;
    }

    private static AbsenceContactNoteDto ToContactNoteDto(
        AbsenceContactNote n,
        string? createdByName,
        string? supersededByName,
        int userId,
        IReadOnlyList<UserRoles> roles) => new()
    {
        Id = n.Id,
        AttendanceRecordId = n.AttendanceRecordId,
        ContactedAt = DateTime.SpecifyKind(n.ContactedAt, DateTimeKind.Utc),
        Channel = n.Channel,
        Outcome = n.Outcome,
        Note = n.Note,
        CreatedByUserId = n.CreatedByUserId,
        CreatedByName = createdByName,
        CreatedAt = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc),
        SupersededAt = n.SupersededAt.HasValue ? DateTime.SpecifyKind(n.SupersededAt.Value, DateTimeKind.Utc) : null,
        SupersededByName = n.SupersededAt != null ? supersededByName : null,
        SupersedeReason = n.SupersedeReason,
        CanSupersede = n.SupersededAt == null
            && (n.CreatedByUserId == userId || roles.Contains(UserRoles.OfficeStaff))
            && (roles.Contains(UserRoles.OfficeStaff) || roles.Contains(UserRoles.Teacher))
    };

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    #endregion

    public async Task<ApiResponse<bool>> RequestReattendanceAsync(
        int classId,
        RequestReattendanceDto dto,
        int requesterUserId,
        CancellationToken ct = default)
    {
        if (dto.Date == default)
        {
            return ApiResponse<bool>.Fail("MSG01", "Ngày điểm danh là bắt buộc.");
        }

        var targetDate = dto.Date.Date;
        if (targetDate > _clock.Today)
        {
            return ApiResponse<bool>.Fail("MSG01", "Không thể yêu cầu điểm danh lại cho ngày trong tương lai.");
        }

        var reason = dto.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 5 || reason.Length > 500)
        {
            return ApiResponse<bool>.Fail("MSG01", "Vui lòng nhập lý do yêu cầu điểm danh lại (từ 5 đến 500 ký tự).");
        }

        var cls = await _unitOfWork.Repository<Class>()
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classId, ct);

        if (cls == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG43);
        }

        // BUG-ATT-05: Chỉ GVCN của lớp mới được gửi yêu cầu điểm danh lại
        var (canRecord, _) = await CanRecordAsync(classId, targetDate, requesterUserId, ct);
        if (!canRecord)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG10);
        }

        var schoolYearId = cls.SchoolYearId > 0 ? cls.SchoolYearId : (int?)null;
        if (!await ValidateSchoolDayAsync(targetDate, schoolYearId, ct))
        {
            return ApiResponse<bool>.Fail("MSG01", "Ngày đã chọn không phải ngày học.");
        }

        // Nếu là hôm nay, chỉ cho phép gửi yêu cầu khi bảng đã bị khóa (GV đã nộp)
        if (targetDate == _clock.Today)
        {
            var todayRecords = await _unitOfWork.Attendance.GetDailyClassAttendanceAsync(classId, targetDate, dto.Session, track: false, ct);
            var hasSubmitted = todayRecords != null && todayRecords.Any(IsTeacherSubmittedRecord);
            if (!hasSubmitted)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Bảng điểm danh hôm nay chưa bị khóa; bạn có thể chấm trực tiếp không cần gửi yêu cầu mở khóa." });
            }
        }

        var officeStaffUserIds = await _unitOfWork.Repository<User>()
            .Query()
            .AsNoTracking()
            .Where(u => u.Status == AccountStatus.Active && u.UserRoles.Any(ur => ur.Role.Name == "OfficeStaff"))
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync(ct);

        if (officeStaffUserIds.Count == 0)
        {
            return ApiResponse<bool>.Fail("MSG01", "Không tìm thấy tài khoản Giáo vụ đang hoạt động để tiếp nhận yêu cầu.");
        }

        var requester = await _unitOfWork.Repository<User>()
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == requesterUserId, ct);

        var utcNow = DateTime.UtcNow;
        var reqRepo = _unitOfWork.Repository<AttendanceReattendanceRequest>();
        if (reqRepo != null)
        {
            var (unlockedReq, pendingReq) = await GetActiveReattendanceStateAsync(classId, targetDate, dto.Session, ct, track: true);
            if (unlockedReq != null)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Bảng điểm danh này đã được Giáo vụ mở khóa; bạn có thể chỉnh sửa trực tiếp ngay bây giờ." });
            }

            if (pendingReq != null)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Đã có yêu cầu điểm danh lại đang chờ Giáo vụ phê duyệt cho buổi này." });
            }

            await reqRepo.AddAsync(new AttendanceReattendanceRequest
            {
                ClassId = classId,
                Date = targetDate,
                Session = dto.Session,
                RequestedByUserId = requesterUserId,
                Reason = reason,
                Status = ReattendanceRequestStatus.Pending,
                CreatedAt = utcNow
            }, ct);
        }

        var sessionLabel = dto.Session == AttendanceSession.Morning ? "Buổi sáng" : "Buổi chiều";
        var requesterName = requester?.FullName ?? "Giáo viên";

        foreach (var staffUserId in officeStaffUserIds)
        {
            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                UserId = staffUserId,
                Type = "AttendanceReattendanceRequest",
                Title = $"GV yêu cầu mở khóa điểm danh lớp {cls.ClassName}",
                Content = $"{requesterName} yêu cầu Giáo vụ mở khóa điểm danh {sessionLabel} ngày {targetDate:dd-MM-yyyy} cho lớp {cls.ClassName}. Lý do: {reason}",
                LinkUrl = $"/teacher/attendance?classId={classId}&date={targetDate:yyyy-MM-dd}&session={dto.Session}",
                ReferenceKey = $"reattendance:{classId}:{targetDate:yyyy-MM-dd}:{dto.Session}:{staffUserId}:{utcNow.Ticks}",
                CreatedAt = utcNow,
                IsRead = false
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(
            requesterUserId,
            AuditActions.AttendanceReattendanceRequest,
            nameof(Class),
            classId.ToString(),
            null,
            new { ClassId = classId, Date = targetDate.ToString("yyyy-MM-dd"), Session = dto.Session.ToString(), Reason = reason, NotifiedStaff = officeStaffUserIds.Count },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đã gửi yêu cầu mở khóa điểm danh lại tới Phòng Giáo vụ.");
    }

    public async Task<ApiResponse<List<ReattendanceRequestDto>>> GetReattendanceRequestsAsync(
        string? status = null,
        CancellationToken ct = default)
    {
        var reqRepo = _unitOfWork.Repository<AttendanceReattendanceRequest>();
        if (reqRepo == null)
        {
            return ApiResponse<List<ReattendanceRequestDto>>.Ok(new List<ReattendanceRequestDto>());
        }

        var query = reqRepo.Query()
            .AsNoTracking()
            .Include(r => r.Class)
            .Include(r => r.RequestedByUser)
            .Include(r => r.ReviewedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReattendanceRequestStatus>(status.Trim(), true, out var parsedStatus))
        {
            query = query.Where(r => r.Status == parsedStatus);
        }

        var rows = await query
            .OrderBy(r => r.Status == ReattendanceRequestStatus.Pending ? 0 : r.Status == ReattendanceRequestStatus.Unlocked ? 1 : 2)
            .ThenByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var result = rows.Select(MapReattendanceToDto).ToList();
        return ApiResponse<List<ReattendanceRequestDto>>.Ok(result);
    }

    public async Task<ApiResponse<ReattendanceRequestDto>> UnlockAttendanceSheetAsync(
        int classId,
        UnlockAttendanceSheetDto dto,
        int staffUserId,
        CancellationToken ct = default)
    {
        if (dto.Date == default)
        {
            return ApiResponse<ReattendanceRequestDto>.Fail(SystemMessages.MSG01, new List<string> { "Ngày điểm danh là bắt buộc." });
        }

        var targetDate = dto.Date.Date;
        if (targetDate > _clock.Today)
        {
            return ApiResponse<ReattendanceRequestDto>.Fail(SystemMessages.MSG01, new List<string> { "Không thể mở khóa điểm danh cho ngày trong tương lai." });
        }

        var trimmedNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        if (trimmedNote != null && trimmedNote.Length > 500)
        {
            return ApiResponse<ReattendanceRequestDto>.Fail(SystemMessages.MSG01, new List<string> { "Ghi chú mở khóa tối đa 500 ký tự." });
        }

        var cls = await _unitOfWork.Repository<Class>()
            .Query()
            .Include(c => c.HomeroomTeacher)
            .FirstOrDefaultAsync(c => c.Id == classId, ct);
        if (cls == null)
        {
            return ApiResponse<ReattendanceRequestDto>.Fail(SystemMessages.MSG43);
        }

        var schoolYearId = cls.SchoolYearId > 0 ? cls.SchoolYearId : (int?)null;
        if (!await ValidateSchoolDayAsync(targetDate, schoolYearId, ct))
        {
            return ApiResponse<ReattendanceRequestDto>.Fail(SystemMessages.MSG01, new List<string> { "Ngày đã chọn không phải ngày học." });
        }

        var reqRepo = _unitOfWork.Repository<AttendanceReattendanceRequest>();
        if (reqRepo == null)
        {
            return ApiResponse<ReattendanceRequestDto>.Fail(SystemMessages.MSG50);
        }

        AttendanceReattendanceRequest? targetReq = null;
        if (dto.RequestId.HasValue && dto.RequestId.Value > 0)
        {
            targetReq = await reqRepo.Query()
                .Include(r => r.Class)
                .Include(r => r.RequestedByUser)
                .Include(r => r.ReviewedByUser)
                .FirstOrDefaultAsync(r => r.Id == dto.RequestId.Value && r.ClassId == classId, ct);
        }

        if (targetReq == null)
        {
            var (unlockedExisting, pendingExisting) = await GetActiveReattendanceStateAsync(classId, targetDate, dto.Session, ct, track: true);
            targetReq = unlockedExisting ?? pendingExisting;
        }

        if (targetReq != null && targetReq.Status == ReattendanceRequestStatus.Unlocked)
        {
            return ApiResponse<ReattendanceRequestDto>.Ok(MapReattendanceToDto(targetReq), "Bảng điểm danh này hiện đã ở trạng thái mở khóa.");
        }

        var utcNow = DateTime.UtcNow;
        if (targetReq != null && targetReq.Status == ReattendanceRequestStatus.Pending)
        {
            targetReq.Status = ReattendanceRequestStatus.Unlocked;
            targetReq.ReviewedByUserId = staffUserId;
            targetReq.ReviewNote = trimmedNote;
            targetReq.UnlockedAt = utcNow;
        }
        else
        {
            targetReq = new AttendanceReattendanceRequest
            {
                ClassId = classId,
                Date = targetDate,
                Session = dto.Session,
                RequestedByUserId = null,
                Reason = trimmedNote ?? "Giáo vụ mở khóa bảng điểm danh cả lớp cho giáo viên chỉnh sửa",
                Status = ReattendanceRequestStatus.Unlocked,
                ReviewedByUserId = staffUserId,
                ReviewNote = trimmedNote,
                CreatedAt = utcNow,
                UnlockedAt = utcNow,
                Class = cls
            };
            await reqRepo.AddAsync(targetReq, ct);
        }

        // Notify requester and homeroom teacher so Teacher knows the whole sheet is unlocked
        var recipientIds = new HashSet<int>();
        if (targetReq.RequestedByUserId.HasValue)
        {
            recipientIds.Add(targetReq.RequestedByUserId.Value);
        }
        if (cls.HomeroomTeacher != null && cls.HomeroomTeacher.UserId > 0)
        {
            recipientIds.Add(cls.HomeroomTeacher.UserId);
        }

        var notifRepo = _unitOfWork.Repository<Notification>();
        var userRepo = _unitOfWork.Repository<User>();
        var staffUser = userRepo != null ? await userRepo.GetByIdAsync(staffUserId, ct) : null;
        if (notifRepo != null && recipientIds.Count > 0)
        {
            var sessionLabel = dto.Session == AttendanceSession.Morning ? "Buổi sáng" : "Buổi chiều";
            foreach (var recipientId in recipientIds)
            {
                await notifRepo.AddAsync(new Notification
                {
                    UserId = recipientId,
                    Type = "AttendanceSheetUnlocked",
                    Title = $"Đã mở khóa điểm danh lớp {cls.ClassName}",
                    Content = $"{staffUser?.FullName ?? "Phòng Giáo vụ"} đã mở khóa bảng điểm danh {sessionLabel} ngày {targetDate:dd-MM-yyyy} lớp {cls.ClassName} để giáo viên chỉnh sửa toàn bộ lớp.",
                    LinkUrl = $"/teacher/attendance?classId={classId}&date={targetDate:yyyy-MM-dd}&session={dto.Session}",
                    ReferenceKey = $"attendance-unlock:{classId}:{targetDate:yyyy-MM-dd}:{dto.Session}:{recipientId}:{utcNow.Ticks}",
                    CreatedAt = utcNow,
                    IsRead = false
                }, ct);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(
            staffUserId,
            AuditActions.AttendanceSheetUnlock,
            nameof(Class),
            classId.ToString(),
            null,
            new { ClassId = classId, Date = targetDate.ToString("yyyy-MM-dd"), Session = dto.Session.ToString(), Note = trimmedNote, RequestId = targetReq.Id },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        targetReq.Class = cls;
        targetReq.ReviewedByUser ??= staffUser;
        return ApiResponse<ReattendanceRequestDto>.Ok(MapReattendanceToDto(targetReq), $"Đã mở khóa bảng điểm danh lớp {cls.ClassName} cho giáo viên.");
    }

    public async Task<ApiResponse<bool>> LockAttendanceSheetAsync(
        int classId,
        LockAttendanceSheetDto dto,
        int staffUserId,
        CancellationToken ct = default)
    {
        if (dto.Date == default)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Ngày điểm danh là bắt buộc." });
        }

        var targetDate = dto.Date.Date;
        var (unlockedReq, _) = await GetActiveReattendanceStateAsync(classId, targetDate, dto.Session, ct, track: true);
        if (unlockedReq == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Bảng điểm danh này hiện không ở trạng thái mở khóa." });
        }

        var utcNow = DateTime.UtcNow;
        unlockedReq.Status = ReattendanceRequestStatus.Completed;
        unlockedReq.CompletedAt = utcNow;
        unlockedReq.ReviewedByUserId = staffUserId;
        if (!string.IsNullOrWhiteSpace(dto.Note))
        {
            unlockedReq.ReviewNote = dto.Note.Trim();
        }

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(
            staffUserId,
            AuditActions.AttendanceSheetLock,
            nameof(Class),
            classId.ToString(),
            null,
            new { ClassId = classId, Date = targetDate.ToString("yyyy-MM-dd"), Session = dto.Session.ToString(), RequestId = unlockedReq.Id },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đã khóa lại bảng điểm danh.");
    }

    public async Task<ApiResponse<ReattendanceRequestDto>> RejectReattendanceRequestAsync(
        int requestId,
        RejectReattendanceRequestDto dto,
        int staffUserId,
        CancellationToken ct = default)
    {
        var reqRepo = _unitOfWork.Repository<AttendanceReattendanceRequest>();
        if (reqRepo == null)
        {
            return ApiResponse<ReattendanceRequestDto>.Fail(SystemMessages.MSG43);
        }

        var req = await reqRepo.Query()
            .Include(r => r.Class)
            .Include(r => r.RequestedByUser)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (req == null)
        {
            return ApiResponse<ReattendanceRequestDto>.Fail(SystemMessages.MSG43);
        }

        if (req.Status != ReattendanceRequestStatus.Pending)
        {
            return ApiResponse<ReattendanceRequestDto>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ có thể từ chối yêu cầu đang chờ xử lý." });
        }

        var trimmedNote = string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : dto.Note.Trim();
        if (trimmedNote.Length < 5 || trimmedNote.Length > 500)
        {
            return ApiResponse<ReattendanceRequestDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập lý do từ chối từ 5 đến 500 ký tự." });
        }

        var utcNow = DateTime.UtcNow;
        req.Status = ReattendanceRequestStatus.Rejected;
        req.ReviewedByUserId = staffUserId;
        req.ReviewNote = trimmedNote;
        req.CompletedAt = utcNow;

        var notifRepo = _unitOfWork.Repository<Notification>();
        var userRepo = _unitOfWork.Repository<User>();
        var staffUser = userRepo != null ? await userRepo.GetByIdAsync(staffUserId, ct) : null;
        if (notifRepo != null && req.RequestedByUserId.HasValue)
        {
            var sessionLabel = req.Session == AttendanceSession.Morning ? "Buổi sáng" : "Buổi chiều";
            await notifRepo.AddAsync(new Notification
            {
                UserId = req.RequestedByUserId.Value,
                Type = "AttendanceReattendanceRejected",
                Title = $"Yêu cầu mở khóa điểm danh lớp {req.Class?.ClassName ?? $"#{req.ClassId}"} bị từ chối",
                Content = $"{staffUser?.FullName ?? "Phòng Giáo vụ"} chưa mở khóa bảng điểm danh {sessionLabel} ngày {req.Date:dd-MM-yyyy}." + (trimmedNote != null ? $" Ghi chú: {trimmedNote}" : ""),
                LinkUrl = $"/teacher/attendance?classId={req.ClassId}&date={req.Date:yyyy-MM-dd}&session={req.Session}",
                ReferenceKey = $"reattendance-reject:{req.Id}:{utcNow.Ticks}",
                CreatedAt = utcNow,
                IsRead = false
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(
            staffUserId,
            AuditActions.AttendanceReattendanceReject,
            nameof(Class),
            req.ClassId.ToString(),
            null,
            new { RequestId = req.Id, req.ClassId, Date = req.Date.ToString("yyyy-MM-dd"), Session = req.Session.ToString(), Note = trimmedNote },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        req.ReviewedByUser ??= staffUser;
        return ApiResponse<ReattendanceRequestDto>.Ok(MapReattendanceToDto(req), "Đã từ chối yêu cầu mở khóa điểm danh.");
    }

    private async Task<(AttendanceReattendanceRequest? Unlocked, AttendanceReattendanceRequest? Pending)> GetActiveReattendanceStateAsync(
        int classId,
        DateTime targetDate,
        AttendanceSession session,
        CancellationToken ct,
        bool track = false)
    {
        var reqRepo = _unitOfWork.Repository<AttendanceReattendanceRequest>();
        if (reqRepo == null) return (null, null);

        var query = reqRepo.Query()
            .Include(r => r.Class)
            .Include(r => r.RequestedByUser)
            .Include(r => r.ReviewedByUser)
            .Where(r => r.ClassId == classId
                        && r.Date == targetDate.Date
                        && r.Session == session
                        && (r.Status == ReattendanceRequestStatus.Unlocked || r.Status == ReattendanceRequestStatus.Pending));

        if (!track)
        {
            query = query.AsNoTracking();
        }

        var list = await query.OrderByDescending(r => r.Id).ToListAsync(ct);
        var unlocked = list.FirstOrDefault(r => r.Status == ReattendanceRequestStatus.Unlocked);
        var pending = list.FirstOrDefault(r => r.Status == ReattendanceRequestStatus.Pending);
        return (unlocked, pending);
    }

    private static ReattendanceRequestDto MapReattendanceToDto(AttendanceReattendanceRequest r) => new()
    {
        Id = r.Id,
        ClassId = r.ClassId,
        ClassName = r.Class?.ClassName ?? $"Lớp #{r.ClassId}",
        Date = r.Date,
        Session = r.Session,
        RequestedByUserId = r.RequestedByUserId,
        RequesterName = r.RequestedByUser?.FullName ?? (r.RequestedByUserId == null ? "Phòng Giáo vụ" : "Giáo viên"),
        Reason = r.Reason,
        Status = r.Status,
        ReviewedByUserId = r.ReviewedByUserId,
        ReviewedByName = r.ReviewedByUser?.FullName,
        ReviewNote = r.ReviewNote,
        CreatedAt = UtcDateTimeHelper.Normalize(r.CreatedAt),
        UnlockedAt = UtcDateTimeHelper.Normalize(r.UnlockedAt),
        CompletedAt = UtcDateTimeHelper.Normalize(r.CompletedAt)
    };
}
