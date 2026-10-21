using System.Text.Json;
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

        // Nếu là Admin, OfficeStaff hoặc SchoolBoard: xem và điểm danh được tất cả các lớp Active của trường
        if (roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.OfficeStaff) || roles.Contains(UserRoles.SchoolBoard))
        {
            var allClasses = await _unitOfWork.Repository<Class>()
                .FindAsync(c => c.Status == "Active", ct);

            var submittedIds = await _unitOfWork.Attendance
                .GetSubmittedClassIdsAsync(allClasses.Select(c => c.Id), targetDate, session, ct);

            var list = allClasses
                .Select(c => new TeacherClassOptionDto
                {
                    ClassId = c.Id,
                    ClassName = c.ClassName,
                    Grade = c.Grade,
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

        // Lấy các lớp GV là GVCN
        var homeroomClasses = await _unitOfWork.Repository<Class>()
            .FindAsync(c => c.HomeroomTeacherId == staff.Id && c.Status == "Active", ct);

        // Lấy các lớp GV dạy tiết đầu trong ngày
        var firstPeriodClassIds = await _unitOfWork.Schedules
            .GetClassesWhereTeacherHasFirstPeriodAsync(staff.Id, targetDate, ct);
        var firstPeriodClasses = await _unitOfWork.Repository<Class>()
            .FindAsync(c => firstPeriodClassIds.Contains(c.Id) && c.Status == "Active", ct);

        // Hợp 2 tập (ưu tiên Homeroom)
        var classDict = new Dictionary<int, TeacherClassOptionDto>();

        foreach (var c in homeroomClasses)
        {
            classDict[c.Id] = new TeacherClassOptionDto
            {
                ClassId = c.Id,
                ClassName = c.ClassName,
                Grade = c.Grade,
                AccessReason = "Homeroom"
            };
        }

        foreach (var c in firstPeriodClasses)
        {
            if (!classDict.ContainsKey(c.Id))
            {
                classDict[c.Id] = new TeacherClassOptionDto
                {
                    ClassId = c.Id,
                    ClassName = c.ClassName,
                    Grade = c.Grade,
                    AccessReason = "FirstPeriod"
                };
            }
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

        var isSchoolDay = await ValidateSchoolDayAsync(targetDate, ct);

        var roster = await _unitOfWork.Students.GetActiveRosterAsync(classId, targetDate, ct);
        if (roster.Count == 0)
        {
            return ApiResponse<AttendanceSheetDto>.Fail(SystemMessages.MSG43);
        }

        var studentIds = roster.Select(s => s.Id).ToList();

        var existingRecords = (await _unitOfWork.Attendance
            .GetDailyClassAttendanceAsync(classId, targetDate, session, track: false, ct))
            .ToDictionary(r => r.StudentId);

        var leaves = await _unitOfWork.Attendance
            .GetActiveLeavesCoveringDateAsync(studentIds, targetDate, session, ct);

        var lunches = await _unitOfWork.Lunch
            .GetRegistrationsForStudentsAsync(studentIds, targetDate, track: false, ct);

        // Xác định quyền sửa & lý do khóa (D1)
        bool canEdit = false;
        string? lockReason = null;

        var isAdminOrOffice = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.OfficeStaff);

        if (isAdminOrOffice)
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
            else
            {
                // Admin / Giáo vụ được mở khóa toàn quyền sửa bất kỳ lớp nào (cả hôm nay lẫn ngày đã qua)
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
            canEdit = false;
            lockReason = "PastDay";
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

        var entries = new List<AttendanceSheetEntryDto>();
        foreach (var s in roster)
        {
            existingRecords.TryGetValue(s.Id, out var rec);
            leaves.TryGetValue(s.Id, out var leave);
            lunches.TryGetValue(s.Id, out var lunch);

            var suggestedStatus = (leave?.Status == RequestStatus.Approved)
                ? AttendanceStatus.ExcusedAbsence
                : AttendanceStatus.Present;

            entries.Add(new AttendanceSheetEntryDto
            {
                StudentId = s.Id,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                AvatarUrl = null,
                RecordId = rec?.Id,
                Status = rec?.Status,
                SuggestedStatus = suggestedStatus,
                Remarks = rec?.Remarks,
                Leave = leave,
                HasLunch = lunch != null && lunch.IsActive,
                LunchCancelledBy = (lunch != null && !lunch.IsActive) ? lunch.CancellationSource : null
            });
        }

        var summary = BuildSummary(entries);

        DateTime? submittedAt = existingRecords.Count > 0 ? existingRecords.Values.Min(r => r.CreatedAt) : null;
        DateTime? lastUpdatedAt = existingRecords.Count > 0 ? existingRecords.Values.Max(r => r.UpdatedAt ?? r.CreatedAt) : null;
        string? submittedByName = null;

        if (existingRecords.Count > 0)
        {
            var earliest = existingRecords.Values.OrderBy(r => r.CreatedAt).First();
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(earliest.RecordedByUserId, ct);
            submittedByName = user?.FullName;
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
            SubmittedAt = submittedAt,
            SubmittedByName = submittedByName,
            LastUpdatedAt = lastUpdatedAt,
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
        var targetDate = dto.Date.Date;
        var session = dto.Session;
        var now = _clock.Now;
        var isAdminOrOffice = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.OfficeStaff);

        // V1: Validate ngày (Admin / OfficeStaff được phép sửa ngày quá khứ)
        if (targetDate < _clock.Today && !isAdminOrOffice)
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG19);
        }
        if (targetDate > _clock.Today)
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Không thể điểm danh cho ngày tương lai." });
        }

        if (!Enum.IsDefined(typeof(AttendanceSession), session))
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Buổi điểm danh không hợp lệ." });
        }

        // V2: Validate ngày học
        if (!await ValidateSchoolDayAsync(targetDate, ct))
        {
            return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Ngày đã chọn không phải ngày học." });
        }

        // V3: Validate quyền điểm danh (Admin / OfficeStaff được toàn quyền sửa cho mọi lớp)
        if (!isAdminOrOffice)
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

        // V6: Validate ExcusedAbsence phải có đơn Approved
        var leaves = await _unitOfWork.Attendance.GetActiveLeavesCoveringDateAsync(rosterIds, targetDate, session, ct);
        foreach (var entry in dto.Entries)
        {
            if (entry.Status == AttendanceStatus.ExcusedAbsence)
            {
                if (!leaves.TryGetValue(entry.StudentId, out var leave) || leave.Status != RequestStatus.Approved)
                {
                    var studentCode = rosterMap.TryGetValue(entry.StudentId, out var s) ? s.StudentCode : entry.StudentId.ToString();
                    return ApiResponse<AttendanceSubmitResultDto>.Fail(SystemMessages.MSG01, new List<string> { $"Học sinh {studentCode}: vắng có phép cần đơn đã được phê duyệt." });
                }
            }
        }

        // DIFF: So khớp thay đổi
        var existingRecords = (await _unitOfWork.Attendance
            .GetDailyClassAttendanceAsync(classId, targetDate, session, track: true, ct))
            .ToDictionary(r => r.StudentId);

        var changes = new List<(int StudentId, AttendanceStatus? OldStatus, AttendanceStatus NewStatus, bool RemarksChanged)>();
        foreach (var entry in dto.Entries)
        {
            existingRecords.TryGetValue(entry.StudentId, out var existing);
            var oldStatus = existing?.Status;
            var remarksChanged = existing != null && existing.Remarks != entry.Remarks;

            if (existing == null || oldStatus != entry.Status || remarksChanged)
            {
                changes.Add((entry.StudentId, oldStatus, entry.Status, remarksChanged));
            }
        }

        var newlyUnexcused = changes.Where(c => c.NewStatus == AttendanceStatus.UnexcusedAbsence && c.OldStatus != AttendanceStatus.UnexcusedAbsence).Select(c => c.StudentId).ToList();
        var newlyExcused = changes.Where(c => c.NewStatus == AttendanceStatus.ExcusedAbsence && c.OldStatus != AttendanceStatus.ExcusedAbsence).Select(c => c.StudentId).ToList();
        var leftExcused = changes.Where(c => c.OldStatus == AttendanceStatus.ExcusedAbsence && (c.NewStatus == AttendanceStatus.Present || c.NewStatus == AttendanceStatus.Late)).Select(c => c.StudentId).ToList();

        var result = new AttendanceSubmitResultDto();

        // TRANSACTION GHI DB
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var utcNow = DateTime.UtcNow;

            // T1: Upsert AttendanceRecord
            foreach (var entry in dto.Entries)
            {
                int? leaveRequestId = (entry.Status == AttendanceStatus.ExcusedAbsence && leaves.TryGetValue(entry.StudentId, out var l))
                    ? l.LeaveRequestId : null;

                if (existingRecords.TryGetValue(entry.StudentId, out var rec))
                {
                    rec.Status = entry.Status;
                    rec.Remarks = entry.Remarks;
                    rec.LeaveRequestId = leaveRequestId;
                    rec.UpdatedAt = utcNow;
                    rec.UpdatedByUserId = userId;
                }
                else
                {
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
                var cutoff = _options.LunchCutoff;

                // Hủy suất ăn CHỈ cho HS mới chuyển sang vắng có phép
                foreach (var id in newlyExcused)
                {
                    if (lunchRegs.TryGetValue(id, out var r) && r.IsActive)
                    {
                        r.IsActive = false;
                        r.CancellationSource = LunchCancellationSource.Attendance;
                        r.CancelledAt = utcNow;
                        r.CancelledByUserId = userId;
                        r.CancellationReason = $"Vắng (có phép) – điểm danh {targetDate:dd/MM}";
                        result.LunchDeductedCount++;
                    }
                }

                // Khôi phục suất ăn cho HS chuyển từ vắng có phép -> đi học/đi muộn,
                // hoặc HS có đơn nghỉ đã hủy suất nhưng thực tế vẫn đi học
                foreach (var id in attendingIds)
                {
                    if (!lunchRegs.TryGetValue(id, out var r) || r.IsActive) continue;

                    var cancelledByLeftExcused = leftExcused.Contains(id) && r.CancellationSource == LunchCancellationSource.Attendance;
                    var cancelledByLeave = r.CancellationSource == LunchCancellationSource.Leave;
                    if (cancelledByLeftExcused || cancelledByLeave)
                    {
                        var studentName = rosterMap.TryGetValue(id, out var st) ? st.FullName : $"HS #{id}";
                        if (cancelledByLeave)
                        {
                            result.Warnings.Add($"{studentName}: có đơn nghỉ đã hủy suất ăn nhưng thực tế đi học.");
                        }

                        if (now.TimeOfDay < cutoff)
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
                            result.Warnings.Add($"{studentName}: Bếp đã chốt suất lúc {_options.LunchCutoffTime}, không khôi phục được suất ăn.");
                        }
                    }
                }
            }

            // T3: Gửi thông báo cho phụ huynh (chỉ newlyUnexcused)
            if (newlyUnexcused.Count > 0)
            {
                var guardians = await _unitOfWork.Students.GetGuardianUserIdsAsync(newlyUnexcused, ct);
                var keys = newlyUnexcused.Select(id => NotificationTemplates.AbsentKey(id, targetDate)).ToList();
                var allGuardianUserIds = guardians.Values.SelectMany(u => u).Distinct().ToList();
                var existingKeys = await _unitOfWork.Notifications.GetExistingKeysAsync(allGuardianUserIds, keys, ct);

                var cls = await _unitOfWork.Repository<Class>().GetByIdAsync(classId, ct);
                var className = cls?.ClassName ?? string.Empty;

                foreach (var studentId in newlyUnexcused)
                {
                    if (guardians.TryGetValue(studentId, out var guardianUserIds))
                    {
                        var studentName = rosterMap.TryGetValue(studentId, out var st) ? st.FullName : $"HS #{studentId}";
                        var key = NotificationTemplates.AbsentKey(studentId, targetDate);

                        foreach (var guardianUserId in guardianUserIds)
                        {
                            if (existingKeys.Contains($"{guardianUserId}|{key}")) continue;

                            var notification = new Notification
                            {
                                UserId = guardianUserId,
                                Type = NotificationTemplates.AttendanceAbsentType,
                                Title = NotificationTemplates.AttendanceAbsentTitle,
                                Content = string.Format(NotificationTemplates.AttendanceAbsentContent, studentName, className, $"{targetDate:dd/MM/yyyy}"),
                                LinkUrl = $"/parent/attendance?studentId={studentId}&month={targetDate:yyyy-MM}",
                                ReferenceKey = key,
                                CreatedAt = utcNow,
                                IsRead = false
                            };
                            await _unitOfWork.Repository<Notification>().AddAsync(notification, ct);
                            result.ParentsNotifiedCount++;
                        }
                    }
                }
            }

            // T4: Lưu thay đổi
            await _unitOfWork.SaveChangesAsync(ct);

            // T5: Audit log
            if (changes.Count > 0)
            {
                var action = existingRecords.Count == 0 ? "ATTENDANCE_SUBMIT" : "ATTENDANCE_UPDATE";
                var oldValuesJson = JsonSerializer.Serialize(changes.Select(c => new { c.StudentId, Status = c.OldStatus?.ToString() }));
                var newValuesJson = JsonSerializer.Serialize(changes.Select(c => new { c.StudentId, Status = c.NewStatus.ToString() }));
                await _auditService.LogActionAsync(userId, action, "AttendanceRecord", $"{classId}:{targetDate:yyyy-MM-dd}:{session}", oldValuesJson, newValuesJson, null, ct);
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
            var leaves = await _unitOfWork.Attendance.GetActiveLeavesCoveringDateAsync(new[] { rec.StudentId }, rec.Date, rec.Session, ct);
            if (!leaves.TryGetValue(rec.StudentId, out var leave) || leave.Status != RequestStatus.Approved)
            {
                return ApiResponse<AttendanceSheetEntryDto>.Fail(SystemMessages.MSG01, new List<string> { "Vắng có phép cần đơn nghỉ phép đã được phê duyệt." });
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
            var cutoff = _options.LunchCutoff;
            var lunchRegs = await _unitOfWork.Lunch.GetRegistrationsForStudentsAsync(new[] { rec.StudentId }, rec.Date, track: true, ct);

            if (rec.Session == AttendanceSession.Morning && lunchRegs.TryGetValue(rec.StudentId, out var lunch))
            {
                if (dto.Status == AttendanceStatus.ExcusedAbsence)
                {
                    if (lunch.IsActive)
                    {
                        lunch.IsActive = false;
                        lunch.CancellationSource = LunchCancellationSource.Staff;
                        lunch.CancelledAt = utcNow;
                        lunch.CancelledByUserId = userId;
                        lunch.CancellationReason = $"Điều chỉnh điểm danh (Vắng có phép): {reason}";
                    }
                }
                else if (oldStatus == AttendanceStatus.ExcusedAbsence && (dto.Status == AttendanceStatus.Present || dto.Status == AttendanceStatus.Late || dto.Status == AttendanceStatus.UnexcusedAbsence))
                {
                    if (!lunch.IsActive && (lunch.CancellationSource == LunchCancellationSource.Attendance || lunch.CancellationSource == LunchCancellationSource.Staff))
                    {
                        if (rec.Date == _clock.Today && _clock.Now.TimeOfDay < cutoff)
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

            // Gửi thông báo phụ huynh nếu chuyển SANG UnexcusedAbsence và trong đúng ngày hôm nay
            if (dto.Status == AttendanceStatus.UnexcusedAbsence && oldStatus != AttendanceStatus.UnexcusedAbsence && rec.Date == _clock.Today)
            {
                var guardians = await _unitOfWork.Students.GetGuardianUserIdsAsync(new[] { rec.StudentId }, ct);
                var key = NotificationTemplates.AbsentKey(rec.StudentId, rec.Date);
                if (guardians.TryGetValue(rec.StudentId, out var guardianUserIds))
                {
                    var existingKeys = await _unitOfWork.Notifications.GetExistingKeysAsync(guardianUserIds, new[] { key }, ct);
                    foreach (var gUserId in guardianUserIds)
                    {
                        if (existingKeys.Contains($"{gUserId}|{key}")) continue;

                        var notification = new Notification
                        {
                            UserId = gUserId,
                            Type = NotificationTemplates.AttendanceAbsentType,
                            Title = NotificationTemplates.AttendanceAbsentTitle,
                            Content = string.Format(NotificationTemplates.AttendanceAbsentContent, rec.Student.FullName, rec.Class.ClassName, $"{rec.Date:dd/MM/yyyy}"),
                            LinkUrl = $"/parent/attendance?studentId={rec.StudentId}&month={rec.Date:yyyy-MM}",
                            ReferenceKey = key,
                            CreatedAt = utcNow,
                            IsRead = false
                        };
                        await _unitOfWork.Repository<Notification>().AddAsync(notification, ct);
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);

            var oldJson = JsonSerializer.Serialize(new { Status = oldStatus.ToString(), Remarks = oldRemarks });
            var newJson = JsonSerializer.Serialize(new { Status = dto.Status.ToString(), Remarks = dto.Remarks, CorrectionReason = reason });
            await _auditService.LogActionAsync(userId, "ATTENDANCE_CORRECTION", "AttendanceRecord", rec.Id.ToString(), oldJson, newJson, null, ct);
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

        var rows = await _unitOfWork.Attendance.GetClassStatusCountsAsync(classId, fromDate, toDate, ct);

        // Số ngày học thực tế có bản ghi
        var classRecords = await _unitOfWork.Repository<AttendanceRecord>()
            .FindAsync(r => r.ClassId == classId && r.Date >= fromDate && r.Date <= toDate, ct);
        var schoolDaysCount = classRecords.Select(r => r.Date).Distinct().Count();

        var totals = new AttendanceSummaryDto
        {
            Total = rows.Sum(r => r.Present + r.Late + r.ExcusedAbsence + r.UnexcusedAbsence),
            Present = rows.Sum(r => r.Present),
            Late = rows.Sum(r => r.Late),
            ExcusedAbsence = rows.Sum(r => r.ExcusedAbsence),
            UnexcusedAbsence = rows.Sum(r => r.UnexcusedAbsence),
            NotMarked = 0
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

        var summary = new AttendanceSummaryDto
        {
            Total = days.Count,
            Present = days.Count(d => d.Status == AttendanceStatus.Present),
            Late = days.Count(d => d.Status == AttendanceStatus.Late),
            ExcusedAbsence = days.Count(d => d.Status == AttendanceStatus.ExcusedAbsence),
            UnexcusedAbsence = days.Count(d => d.Status == AttendanceStatus.UnexcusedAbsence),
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
        var staffList = await _unitOfWork.Repository<StaffProfile>()
            .FindAsync(sp => sp.UserId == userId, ct);
        var staff = staffList.FirstOrDefault();

        if (staff == null) return (false, "NotAssigned");

        var cls = await _unitOfWork.Repository<Class>()
            .GetByIdAsync(classId, ct);

        if (cls == null || cls.Status != "Active") return (false, "NotAssigned");

        // Là GVCN
        if (cls.HomeroomTeacherId == staff.Id) return (true, null);

        // Là GV dạy tiết đầu
        var firstTeacherId = await _unitOfWork.Schedules.GetFirstPeriodTeacherIdAsync(classId, date, ct);
        if (firstTeacherId == staff.Id) return (true, null);

        return (false, "NotAssigned");
    }

    /// <summary>
    /// Kiểm tra quyền xem lớp (BGH, Giáo vụ, hoặc GV phụ trách lớp)
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
            if (cls != null && cls.HomeroomTeacherId == staff.Id) return true;

            var schedules = await _unitOfWork.Repository<ClassSchedule>()
                .FindAsync(cs => cs.ClassId == classId && cs.TeacherStaffId == staff.Id && cs.IsActive, ct);

            return schedules.Any();
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra xem một ngày có phải ngày học theo lịch của trường không
    /// </summary>
    private async Task<bool> ValidateSchoolDayAsync(DateTime date, CancellationToken ct = default)
    {
        var calendarDays = await _unitOfWork.Repository<SchoolCalendarDay>()
            .FindAsync(d => d.Date == date, ct);
        var calendarDay = calendarDays.FirstOrDefault();

        if (calendarDay != null)
        {
            return calendarDay.IsSchoolDay;
        }

        // Mặc định Thứ 2 đến Thứ 6 là ngày học
        return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
    }

    #endregion
}
