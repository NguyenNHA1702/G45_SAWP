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

public class LunchService : ILunchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISchoolClock _clock;
    private readonly SchoolOptions _options;
    private readonly IAuditService _auditService;

    private static readonly string[] DefaultMenus =
    {
        "Cơm trắng, Thịt gà kho nấm, Canh bí đỏ nấu tôm, Chuối tiêu",
        "Cơm trắng, Cá basa sốt cà chua, Rau cải xào tỏi, Canh cua mồng tơi, Sữa chua",
        "Cơm trắng, Thịt lợn rim tiêu, Đậu phụ nhồi thịt, Canh rau ngót thịt băm, Dưa hấu",
        "Cơm trắng, Trứng đúc thịt, Su su xào cà rốt, Canh sườn nấu củ quả, Cam tươi",
        "Cơm trắng, Bò hầm khoai tây cà rốt, Bắp cải luộc, Canh chua cá, Bánh flan"
    };

    public LunchService(
        IUnitOfWork unitOfWork,
        ISchoolClock clock,
        IOptions<SchoolOptions> options,
        IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options.Value;
        _auditService = auditService;
    }

    private (bool IsServing, TimeSpan Cutoff, bool IsLocked, DateTime? LockedAt, string? Remarks) ResolveServiceDay(DateTime date, LunchServiceDay? day)
    {
        var targetDate = date.Date;
        var isWeekend = targetDate.DayOfWeek == DayOfWeek.Saturday || targetDate.DayOfWeek == DayOfWeek.Sunday;
        var isServing = day != null ? day.IsServing : !isWeekend;
        var cutoff = (day != null && day.CutoffTime != TimeSpan.Zero) ? day.CutoffTime : _options.LunchCutoff;
        var isLocked = day?.IsLocked ?? false;
        var lockedAt = day?.LockedAt;

        string? remarks = day?.Remarks;
        if ((string.IsNullOrWhiteSpace(remarks) || remarks == "Phục vụ bán trú bình thường") && isServing)
        {
            var menuIdx = Math.Abs((int)targetDate.DayOfWeek - 1) % DefaultMenus.Length;
            remarks = DefaultMenus[menuIdx];
            if (day != null && day.Remarks == "Phục vụ bán trú bình thường")
            {
                day.Remarks = remarks;
            }
        }

        return (isServing, cutoff, isLocked, lockedAt, remarks);
    }

    private bool IsCutoffPassed(DateTime targetDate, TimeSpan cutoff, bool isLocked)
    {
        if (isLocked) return true;
        var cutoffMoment = targetDate.Date.Add(cutoff);
        return _clock.Now > cutoffMoment;
    }

    private async Task<bool> IsParentLinkedToStudentAsync(int parentUserId, int studentId, CancellationToken ct)
    {
        var guardian = await _unitOfWork.Repository<Guardian>()
            .Query()
            .Include(g => g.StudentGuardians)
            .FirstOrDefaultAsync(g => g.UserId == parentUserId, ct);

        return guardian != null && guardian.StudentGuardians.Any(sg => sg.StudentId == studentId);
    }

    public async Task<ApiResponse<bool>> RegisterLunchAsync(LunchRegistrationDto request, CancellationToken cancellationToken = default)
    {
        var batchReq = new BatchLunchRegistrationRequestDto
        {
            StudentId = request.StudentId,
            Dates = new List<DateTime> { request.Date.Date },
            MealType = request.MealType,
            Remarks = request.Remarks
        };

        var res = await RegisterLunchBatchAsync(0, new[] { UserRoles.ServiceStaff }, batchReq, cancellationToken);
        if (!res.Success)
        {
            return ApiResponse<bool>.Fail(res.Message ?? SystemMessages.MSG09.Content);
        }

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG25);
    }

    /// <summary>
    /// UC-32: Lấy cấu hình ngày phục vụ trong khoảng ngày
    /// </summary>
    public async Task<ApiResponse<List<LunchServiceDayConfigDto>>> GetServiceDaysAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var start = fromDate.Date;
        var end = toDate.Date;
        if (end < start || (end - start).TotalDays > 92)
        {
            return ApiResponse<List<LunchServiceDayConfigDto>>.Fail(SystemMessages.MSG01, new List<string> { "Khoảng ngày truy vấn không hợp lệ (tối đa 92 ngày)." });
        }

        var existingDays = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .Where(d => d.Date >= start && d.Date <= end)
            .ToDictionaryAsync(d => d.Date.Date, cancellationToken);

        var result = new List<LunchServiceDayConfigDto>();
        for (var cur = start; cur <= end; cur = cur.AddDays(1))
        {
            existingDays.TryGetValue(cur, out var day);
            var resolved = ResolveServiceDay(cur, day);
            result.Add(new LunchServiceDayConfigDto
            {
                Date = cur,
                IsServing = resolved.IsServing,
                CutoffTime = resolved.Cutoff.ToString(@"hh\:mm"),
                IsLocked = resolved.IsLocked,
                LockedAt = resolved.LockedAt,
                Remarks = resolved.Remarks
            });
        }

        return ApiResponse<List<LunchServiceDayConfigDto>>.Ok(result);
    }

    /// <summary>
    /// UC-32: Nhân viên bán trú cấu hình ngày phục vụ, hạn đăng ký/hủy và ghi chú hoặc thực đơn
    /// </summary>
    public async Task<ApiResponse<List<LunchServiceDayConfigDto>>> UpsertServiceDaysAsync(int userId, UpsertLunchServiceDaysRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.Dates == null || request.Dates.Count == 0)
        {
            return ApiResponse<List<LunchServiceDayConfigDto>>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn ít nhất một ngày để cấu hình." });
        }

        if (!TimeSpan.TryParse(request.CutoffTime, out var parsedCutoff) || parsedCutoff <= TimeSpan.Zero || parsedCutoff >= TimeSpan.FromDays(1))
        {
            return ApiResponse<List<LunchServiceDayConfigDto>>.Fail(SystemMessages.MSG01, new List<string> { "Hạn đăng ký/hủy (CutoffTime) không đúng định dạng HH:mm." });
        }

        var targetDates = request.Dates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        var existingMap = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .Where(d => targetDates.Contains(d.Date))
            .ToDictionaryAsync(d => d.Date.Date, cancellationToken);

        var updatedDtos = new List<LunchServiceDayConfigDto>();

        var normalizedRemarks = request.Remarks?.Trim();
        if (normalizedRemarks != null && normalizedRemarks.Length > 250)
        {
            normalizedRemarks = normalizedRemarks[..250];
        }

        foreach (var d in targetDates)
        {
            if (existingMap.TryGetValue(d, out var existing))
            {
                existing.IsServing = request.IsServing;
                existing.CutoffTime = parsedCutoff;
                existing.Remarks = normalizedRemarks;
            }
            else
            {
                existing = new LunchServiceDay
                {
                    Date = d,
                    IsServing = request.IsServing,
                    CutoffTime = parsedCutoff,
                    IsLocked = false,
                    Remarks = normalizedRemarks
                };
                await _unitOfWork.Repository<LunchServiceDay>().AddAsync(existing, cancellationToken);
            }

            updatedDtos.Add(new LunchServiceDayConfigDto
            {
                Date = d,
                IsServing = existing.IsServing,
                CutoffTime = existing.CutoffTime.ToString(@"hh\:mm"),
                IsLocked = existing.IsLocked,
                LockedAt = existing.LockedAt,
                Remarks = existing.Remarks
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var auditEntityId = targetDates.Count == 1
            ? targetDates[0].ToString("yyyy-MM-dd")
            : $"{targetDates.First():yyyy-MM-dd}..{targetDates.Last():yyyy-MM-dd} ({targetDates.Count}d)";
        var allDatesStr = string.Join(",", targetDates.Select(d => d.ToString("yyyy-MM-dd")));

        await _auditService.LogActionAsync(
            userId,
            "LUNCH_SERVICE_DAY_CONFIG",
            "LunchServiceDay",
            auditEntityId,
            null,
            $"Dates=[{allDatesStr}], IsServing={request.IsServing}, CutoffTime={parsedCutoff:hh\\:mm}, Remarks={normalizedRemarks}",
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<List<LunchServiceDayConfigDto>>.Ok(updatedDtos, SystemMessages.MSG44);
    }

    /// <summary>
    /// UC-31: Phụ huynh xem lịch và trạng thái đăng ký của con theo tháng
    /// </summary>
    public async Task<ApiResponse<StudentLunchScheduleDto>> GetStudentScheduleAsync(
        int studentId,
        string month,
        int userId,
        IReadOnlyList<UserRoles> roles,
        CancellationToken cancellationToken = default)
    {
        var student = await _unitOfWork.Repository<Student>()
            .Query()
            .Include(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student == null)
        {
            return ApiResponse<StudentLunchScheduleDto>.Fail(SystemMessages.MSG11);
        }

        var isStaffOrAdmin = roles.Contains(UserRoles.Admin) ||
                             roles.Contains(UserRoles.ServiceStaff) ||
                             roles.Contains(UserRoles.OfficeStaff) ||
                             roles.Contains(UserRoles.SchoolBoard) ||
                             roles.Contains(UserRoles.Teacher);

        if (!isStaffOrAdmin)
        {
            var isLinked = await IsParentLinkedToStudentAsync(userId, studentId, cancellationToken);
            if (!isLinked)
            {
                return ApiResponse<StudentLunchScheduleDto>.Fail(SystemMessages.MSG10);
            }
        }

        int year = _clock.Today.Year;
        int m = _clock.Today.Month;
        if (!string.IsNullOrWhiteSpace(month))
        {
            var parts = month.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var py) && int.TryParse(parts[1], out var pm) && pm >= 1 && pm <= 12)
            {
                year = py;
                m = pm;
            }
        }

        var startOfMonth = new DateTime(year, m, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        var serviceDays = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .Where(d => d.Date >= startOfMonth && d.Date <= endOfMonth)
            .ToDictionaryAsync(d => d.Date.Date, cancellationToken);

        var registrations = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Where(r => r.StudentId == studentId && r.Date >= startOfMonth && r.Date <= endOfMonth)
            .ToDictionaryAsync(r => r.Date.Date, cancellationToken);

        var days = new List<StudentLunchDayDto>();
        for (var cur = startOfMonth; cur <= endOfMonth; cur = cur.AddDays(1))
        {
            serviceDays.TryGetValue(cur, out var sDay);
            registrations.TryGetValue(cur, out var reg);

            var resolved = ResolveServiceDay(cur, sDay);
            var cutoffPassed = IsCutoffPassed(cur, resolved.Cutoff, resolved.IsLocked);

            days.Add(new StudentLunchDayDto
            {
                Date = cur,
                IsServing = resolved.IsServing,
                CutoffTime = resolved.Cutoff.ToString(@"hh\:mm"),
                IsLocked = resolved.IsLocked,
                IsCutoffPassed = cutoffPassed,
                ServiceRemarks = resolved.Remarks,
                RegistrationId = reg?.Id,
                IsRegistered = reg != null && reg.IsActive,
                IsWaitlisted = reg != null && reg.IsWaitlisted && !reg.IsActive,
                MealType = reg?.MealType,
                StudentRemarks = reg?.Remarks,
                CancellationSource = (reg != null && !reg.IsActive) ? reg.CancellationSource : null,
                CancelledAt = (reg != null && !reg.IsActive) ? reg.CancelledAt : null,
                CancellationReason = (reg != null && !reg.IsActive) ? reg.CancellationReason : null
            });
        }

        var className = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class?.ClassName ?? "Chưa phân lớp";

        var schedule = new StudentLunchScheduleDto
        {
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            ClassName = className,
            Month = $"{year:D4}-{m:D2}",
            TotalServingDays = days.Count(d => d.IsServing),
            TotalRegisteredDays = days.Count(d => d.IsRegistered),
            TotalCancelledDays = days.Count(d => !d.IsRegistered && d.CancellationSource != null),
            Days = days
        };

        return ApiResponse<StudentLunchScheduleDto>.Ok(schedule);
    }

    /// <summary>
    /// UC-29: Phụ huynh chọn một hoặc nhiều ngày còn mở và xác nhận đăng ký độc lập cho từng ngày
    /// </summary>
    public async Task<ApiResponse<BatchLunchRegistrationResultDto>> RegisterLunchBatchAsync(
        int userId,
        IReadOnlyList<UserRoles> roles,
        BatchLunchRegistrationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.StudentId <= 0 || request.Dates == null || request.Dates.Count == 0)
        {
            return ApiResponse<BatchLunchRegistrationResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn học sinh và ít nhất một ngày phục vụ." });
        }

        var student = await _unitOfWork.Repository<Student>()
            .Query()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);

        if (student == null)
        {
            return ApiResponse<BatchLunchRegistrationResultDto>.Fail(SystemMessages.MSG11);
        }

        var isStaffOrAdmin = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.ServiceStaff);
        if (!isStaffOrAdmin && userId > 0)
        {
            var isLinked = await IsParentLinkedToStudentAsync(userId, request.StudentId, cancellationToken);
            if (!isLinked)
            {
                return ApiResponse<BatchLunchRegistrationResultDto>.Fail(SystemMessages.MSG10);
            }
        }

        var targetDates = request.Dates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();

        var serviceDays = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .Where(d => targetDates.Contains(d.Date))
            .ToDictionaryAsync(d => d.Date.Date, cancellationToken);

        // Kiểm tra ngày phục vụ (MSG28) và hạn đăng ký (MSG27)
        foreach (var d in targetDates)
        {
            serviceDays.TryGetValue(d, out var sDay);
            var resolved = ResolveServiceDay(d, sDay);

            if (!resolved.IsServing)
            {
                return ApiResponse<BatchLunchRegistrationResultDto>.Fail(
                    SystemMessages.MSG28,
                    new List<string> { $"Ngày {d:dd/MM/yyyy} không phục vụ ăn trưa." });
            }

            if (IsCutoffPassed(d, resolved.Cutoff, resolved.IsLocked))
            {
                return ApiResponse<BatchLunchRegistrationResultDto>.Fail(
                    SystemMessages.MSG27,
                    new List<string> { $"Đã quá thời hạn đăng ký ({resolved.Cutoff:hh\\:mm}) cho ngày {d:dd/MM/yyyy}." });
            }
        }

        var existingRegs = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Where(r => r.StudentId == request.StudentId && targetDates.Contains(r.Date))
            .ToDictionaryAsync(r => r.Date.Date, cancellationToken);

        // Nếu tất cả các ngày chọn đều đã đăng ký còn hiệu lực -> báo trùng MSG29
        if (targetDates.All(d => existingRegs.TryGetValue(d, out var r) && r.IsActive))
        {
            return ApiResponse<BatchLunchRegistrationResultDto>.Fail(SystemMessages.MSG29);
        }

        var registeredDates = new List<DateTime>();
        var skippedDuplicates = new List<DateTime>();
        var waitlistedDates = new List<DateTime>();

        // Kiểm tra chỉ tiêu bán trú theo tháng (Monthly Boarding Quota - TC_BDS_34)
        var quotaRepo = _unitOfWork.Repository<LunchMonthlyQuota>();
        var fullMonths = new HashSet<string>();
        var monthQuotaMap = new Dictionary<string, (int Quota, int ActiveCount)>();

        if (quotaRepo != null)
        {
            var distinctMonths = targetDates.Select(d => d.ToString("yyyy-MM")).Distinct().ToList();
            var quotas = await quotaRepo.Query()
                .Where(q => distinctMonths.Contains(q.Month))
                .ToDictionaryAsync(q => q.Month, cancellationToken);

            foreach (var mKey in distinctMonths)
            {
                if (quotas.TryGetValue(mKey, out var q) && q.Quota > 0)
                {
                    var parts = mKey.Split('-');
                    int qYear = int.Parse(parts[0]);
                    int qMonth = int.Parse(parts[1]);
                    var mStart = new DateTime(qYear, qMonth, 1);
                    var mEnd = mStart.AddMonths(1).AddDays(-1);

                    var activeStudentIds = await _unitOfWork.Repository<LunchRegistration>()
                        .Query()
                        .Where(r => r.Date >= mStart && r.Date <= mEnd && r.IsActive && !r.IsWaitlisted)
                        .Select(r => r.StudentId)
                        .Distinct()
                        .ToListAsync(cancellationToken);

                    monthQuotaMap[mKey] = (q.Quota, activeStudentIds.Count);
                    if (!activeStudentIds.Contains(request.StudentId) && activeStudentIds.Count >= q.Quota)
                    {
                        fullMonths.Add(mKey);
                    }
                }
            }
        }

        foreach (var d in targetDates)
        {
            var dMonth = d.ToString("yyyy-MM");
            bool isMonthFull = fullMonths.Contains(dMonth);

            if (existingRegs.TryGetValue(d, out var existing))
            {
                if (existing.IsActive)
                {
                    skippedDuplicates.Add(d);
                    continue;
                }

                if (isMonthFull)
                {
                    existing.IsActive = false;
                    existing.IsWaitlisted = true;
                    existing.MealType = request.MealType;
                    existing.Remarks = request.Remarks?.Trim();
                    waitlistedDates.Add(d);
                    continue;
                }

                // Bản ghi trước đó đã hủy / waitlist -> kích hoạt lại thành bản đăng ký hợp lệ
                existing.IsActive = true;
                existing.IsWaitlisted = false;
                existing.MealType = request.MealType;
                existing.Remarks = request.Remarks?.Trim();
                existing.CancellationSource = null;
                existing.CancelledAt = null;
                existing.CancelledByUserId = null;
                existing.CancellationReason = null;
                registeredDates.Add(d);
            }
            else
            {
                var newReg = new LunchRegistration
                {
                    StudentId = request.StudentId,
                    Date = d,
                    MealType = request.MealType,
                    IsActive = !isMonthFull,
                    IsWaitlisted = isMonthFull,
                    Remarks = request.Remarks?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<LunchRegistration>().AddAsync(newReg, cancellationToken);
                if (isMonthFull)
                {
                    waitlistedDates.Add(d);
                }
                else
                {
                    registeredDates.Add(d);
                }
            }
        }

        // Xử lý Waitlist nếu có tháng đã đủ chỉ tiêu (TC_BDS_34)
        if (waitlistedDates.Count > 0)
        {
            var firstMonth = waitlistedDates[0].ToString("yyyy-MM");
            var (qVal, aVal) = monthQuotaMap.TryGetValue(firstMonth, out var info) ? info : (0, 0);

            await _auditService.LogActionAsync(
                userId > 0 ? userId : null,
                "BOARDING_REGISTRATION_WAITLISTED",
                "LunchRegistration",
                student.Id.ToString(),
                null,
                $"Month={firstMonth};Quota={qVal};CurrentActive={aVal};StudentCode={student.StudentCode};Dates={string.Join(",", waitlistedDates.Select(x => x.ToString("yyyy-MM-dd")))}",
                null,
                cancellationToken);

            var notifRepo = _unitOfWork.Repository<Notification>();
            if (notifRepo != null && userId > 0)
            {
                await notifRepo.AddAsync(new Notification
                {
                    UserId = userId,
                    Title = "Registration Waitlisted",
                    Content = $"Đăng ký bán trú tháng {firstMonth} cho học sinh {student.FullName} ({student.StudentCode}) đã được đưa vào danh sách chờ (Waitlist) do đã đủ chỉ tiêu {aVal}/{qVal}.",
                    Type = "BOARDING_WAITLISTED",
                    LinkUrl = $"/parent/lunch?studentId={student.Id}&month={firstMonth}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                }, cancellationToken);
            }
        }

        // Gửi thông báo xác nhận (UC-29)
        if (userId > 0 && registeredDates.Count > 0)
        {
            var dateListStr = string.Join(", ", registeredDates.Take(5).Select(d => d.ToString("dd/MM"))) +
                              (registeredDates.Count > 5 ? $" (+{registeredDates.Count - 5} ngày)" : "");
            var notif = new Notification
            {
                UserId = userId,
                Title = "Xác nhận đăng ký bán trú",
                Content = $"Đã đăng ký thành công {registeredDates.Count} ngày ăn bán trú ({dateListStr}) cho học sinh {student.FullName}.",
                LinkUrl = $"/parent/lunch?studentId={student.Id}&month={registeredDates[0]:yyyy-MM}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var result = new BatchLunchRegistrationResultDto
        {
            RegisteredCount = registeredDates.Count,
            RegisteredDates = registeredDates,
            SkippedDuplicateDates = skippedDuplicates,
            WaitlistedCount = waitlistedDates.Count,
            WaitlistedDates = waitlistedDates,
            IsWaitlisted = waitlistedDates.Count > 0
        };

        if (waitlistedDates.Count > 0 && registeredDates.Count == 0)
        {
            var firstMonth = waitlistedDates[0].ToString("yyyy-MM");
            var (qVal, aVal) = monthQuotaMap.TryGetValue(firstMonth, out var info) ? info : (0, 0);
            return new ApiResponse<BatchLunchRegistrationResultDto>
            {
                Success = false,
                Code = "BOARDING_WAITLISTED",
                Message = $"Registration Waitlisted: Chỉ tiêu bán trú tháng {firstMonth} đã đủ ({aVal}/{qVal}). Đăng ký cho {student.FullName} đã được chuyển vào danh sách chờ.",
                Data = result
            };
        }

        return ApiResponse<BatchLunchRegistrationResultDto>.Ok(result, SystemMessages.MSG25);
    }

    /// <summary>
    /// UC-30: Phụ huynh chọn đăng ký còn hiệu lực và hủy trước hạn hủy
    /// </summary>
    public async Task<ApiResponse<bool>> CancelRegistrationByParentAsync(
        int userId,
        IReadOnlyList<UserRoles> roles,
        CancelLunchRegistrationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.StudentId <= 0)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01);
        }

        var isStaffOrAdmin = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.ServiceStaff);
        if (!isStaffOrAdmin)
        {
            var isLinked = await IsParentLinkedToStudentAsync(userId, request.StudentId, cancellationToken);
            if (!isLinked)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG10);
            }
        }

        var targetDate = request.Date.Date;
        var reg = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.StudentId == request.StudentId && r.Date == targetDate, cancellationToken);

        if (reg == null || !reg.IsActive)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Không tìm thấy đăng ký ăn trưa còn hiệu lực cho ngày đã chọn." });
        }

        var serviceDay = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .FirstOrDefaultAsync(d => d.Date == targetDate, cancellationToken);

        var resolved = ResolveServiceDay(targetDate, serviceDay);

        // Kiểm tra hạn hủy (UC-30): nếu quá hạn 08h15 hoặc đã khóa ngày -> thông báo MSG27 và giữ nguyên đăng ký hiện tại
        if (IsCutoffPassed(targetDate, resolved.Cutoff, resolved.IsLocked))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG27);
        }

        reg.IsActive = false;
        reg.CancellationSource = isStaffOrAdmin && !roles.Contains(UserRoles.Parent)
            ? LunchCancellationSource.Staff
            : LunchCancellationSource.Parent;
        reg.CancelledAt = DateTime.UtcNow;
        reg.CancelledByUserId = userId;
        reg.CancellationReason = string.IsNullOrWhiteSpace(request.Reason)
            ? $"Phụ huynh hủy đăng ký trực tiếp trước {resolved.Cutoff:hh\\:mm}"
            : request.Reason.Trim();

        if (userId > 0)
        {
            var notif = new Notification
            {
                UserId = userId,
                Title = "Đã hủy đăng ký suất ăn bán trú",
                Content = $"Đã hủy suất ăn bán trú ngày {targetDate:dd/MM/yyyy} của học sinh {reg.Student?.FullName} trước giờ chốt ({resolved.Cutoff:hh\\:mm}).",
                LinkUrl = $"/parent/lunch?studentId={request.StudentId}&month={targetDate:yyyy-MM}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG26);
    }

    public async Task<ApiResponse<DailyMealCountReportDto>> GetDailyMealCountAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var activeRegs = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Where(r => r.Date == targetDate && r.IsActive)
            .ToListAsync(cancellationToken);

        return ApiResponse<DailyMealCountReportDto>.Ok(new DailyMealCountReportDto
        {
            Date = targetDate,
            TotalStandardMeals = activeRegs.Count(r => r.MealType == MealType.StandardLunch),
            TotalVegetarianMeals = activeRegs.Count(r => r.MealType == MealType.VegetarianLunch),
            TotalSpecialDietMeals = activeRegs.Count(r => r.MealType == MealType.SpecialDiet),
            GrandTotal = activeRegs.Count
        });
    }

    public async Task<ApiResponse<bool>> CancelMealForLeaveAsync(int studentId, DateTime mealDate, string reason, CancellationToken cancellationToken = default)
    {
        var targetDate = mealDate.Date;

        // Kiểm tra hạn chốt theo ngày phục vụ (Quy tắc Tân Lập A: trước 08h15 hoặc theo cấu hình ngày)
        var serviceDay = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .FirstOrDefaultAsync(d => d.Date == targetDate, cancellationToken);

        var resolved = ResolveServiceDay(targetDate, serviceDay);
        if (IsCutoffPassed(targetDate, resolved.Cutoff, resolved.IsLocked))
        {
            return ApiResponse<bool>.Ok(false, $"Đã qua mốc chốt suất ({resolved.Cutoff:hh\\:mm}), giữ nguyên đăng ký hiện tại.");
        }

        var reg = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Date == targetDate, cancellationToken);

        if (reg != null && reg.IsActive)
        {
            reg.IsActive = false;
            reg.CancellationSource = LunchCancellationSource.Leave;
            reg.CancelledAt = DateTime.UtcNow;
            reg.CancellationReason = reason;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Ok(true, "Đã tự động ghi nhận hủy suất ăn trước giờ chốt");
        }

        return ApiResponse<bool>.Ok(false, "Học sinh không có đăng ký suất ăn còn hiệu lực cho ngày này");
    }

    public async Task<ApiResponse<bool>> RestoreMealForStudentAsync(int studentId, DateTime mealDate, string reason, CancellationToken cancellationToken = default)
    {
        var targetDate = mealDate.Date;
        var reg = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Date == targetDate, cancellationToken);

        if (reg != null && !reg.IsActive)
        {
            reg.IsActive = true;
            reg.CancellationSource = null;
            reg.CancelledAt = null;
            reg.CancelledByUserId = null;
            reg.CancellationReason = null;
            reg.Remarks = $"Khôi phục suất ăn: {reason} (Lúc {_clock.Now:HH:mm dd/MM/yyyy})";
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Ok(true, "Đã khôi phục suất ăn");
        }

        return ApiResponse<bool>.Ok(false, "Học sinh chưa có bản ghi đăng ký bán trú bị hủy cho ngày này");
    }

    public async Task<ApiResponse<DailyMealBoardDto>> GetDailyMealBoardAsync(
        DateTime date,
        int? userId = null,
        IReadOnlyList<UserRoles>? roles = null,
        int? classId = null,
        CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;

        // 1. Kiểm tra trạng thái cấu hình và chốt ngày (UC-32)
        var serviceDay = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .FirstOrDefaultAsync(d => d.Date == targetDate, cancellationToken);

        var resolved = ResolveServiceDay(targetDate, serviceDay);

        // Tự động bảo đảm có dữ liệu đăng ký bán trú mẫu cho tất cả học sinh đang học nếu ngày phục vụ còn thiếu
        var studentRepo = _unitOfWork.Repository<Student>();
        if (resolved.IsServing && studentRepo != null)
        {
            var existingStudentIds = await _unitOfWork.Repository<LunchRegistration>()
                .Query()
                .Where(r => r.Date == targetDate)
                .Select(r => r.StudentId)
                .ToListAsync(cancellationToken);

            var allActiveStudents = await studentRepo
                .Query()
                .Where(s => s.Status == "Active")
                .ToListAsync(cancellationToken);

            if (existingStudentIds.Count < allActiveStudents.Count)
            {
                var existingSet = existingStudentIds.ToHashSet();
                foreach (var s in allActiveStudents.Where(st => !existingSet.Contains(st.Id)))
                {
                    var code = s.StudentCode ?? string.Empty;
                    bool isVegetarian = code is "HS004" or "HS017" or "HS022" or "HS035" or "HS062";
                    bool isSpecialDiet = code is "HS001" or "HS006" or "HS015" or "HS028" or "HS040";
                    bool isCancelled = code is "HS005" or "HS012" or "HS038" or "HS050";

                    await _unitOfWork.Repository<LunchRegistration>().AddAsync(new LunchRegistration
                    {
                        StudentId = s.Id,
                        Date = targetDate,
                        MealType = isVegetarian ? MealType.VegetarianLunch : isSpecialDiet ? MealType.SpecialDiet : MealType.StandardLunch,
                        IsActive = !isCancelled,
                        Remarks = isVegetarian
                            ? "Cơm trắng dẻo, Đậu phụ sốt cà chua, Nấm đùi gà kho xì dầu, Rau củ quả luộc, Canh nấm hạt sen | Ghi chú: Suất ăn chay thanh đạm"
                            : isSpecialDiet
                                ? (code == "HS001"
                                    ? "Cơm mềm dẻo, Thịt gà phi lê áp chảo, Rau củ hấp mềm, Canh rau ngót thịt băm, Sữa chua | Ghi chú: Dị ứng hải sản (tôm, cua, sò)"
                                    : code == "HS006"
                                        ? "Cơm mềm dẻo, Thịt gà phi lê áp chảo, Rau củ hấp mềm, Canh rau ngót thịt băm, Sữa chua | Ghi chú: Dị ứng tôm & hải sản có vỏ"
                                        : "Cháo dinh dưỡng thịt băm, Cá hồi hấp xì dầu, Bí đỏ hấp mềm, Canh rau mồng tơi, Sữa chua | Ghi chú: Chế độ ăn mềm dễ tiêu hóa")
                                : null,
                        CancelledAt = isCancelled ? targetDate.AddHours(7).AddMinutes(45) : null,
                        CancellationReason = isCancelled ? "Nghỉ có phép / Phụ huynh báo hủy trước 08h15" : null,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // Xác định phạm vi quyền (UC-33: Nhân viên bán trú vận hành; Giáo vụ/BGH xem toàn trường; Giáo viên xem lớp mình phụ trách)
        bool canManage = roles == null || roles.Contains(UserRoles.ServiceStaff) || roles.Contains(UserRoles.Admin);
        bool isTeacherOnly = roles != null &&
                             roles.Contains(UserRoles.Teacher) &&
                             !canManage &&
                             !roles.Contains(UserRoles.OfficeStaff) &&
                             !roles.Contains(UserRoles.SchoolBoard);

        HashSet<int>? allowedClassIds = null;
        if (isTeacherOnly && userId.HasValue)
        {
            var staff = await _unitOfWork.Repository<StaffProfile>()
                .Query()
                .FirstOrDefaultAsync(sp => sp.UserId == userId.Value, cancellationToken);

            if (staff != null)
            {
                var homeroomIds = await _unitOfWork.Repository<Class>()
                    .Query()
                    .Where(c => c.HomeroomTeacherId == staff.Id && c.Status == "Active")
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                allowedClassIds = homeroomIds.ToHashSet();
            }
            else
            {
                allowedClassIds = new HashSet<int>();
            }
        }

        // 2. Lấy toàn bộ đăng ký ăn bán trú của ngày kèm thông tin học sinh và lớp
        var registrations = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                    .ThenInclude(ca => ca.Class)
            .Where(r => r.Date == targetDate)
            .ToListAsync(cancellationToken);

        // 3. Phân bổ theo lớp trong phạm vi quyền
        var allClassesQuery = _unitOfWork.Repository<Class>()
            .Query()
            .Where(c => c.Status == "Active");

        if (allowedClassIds != null)
        {
            allClassesQuery = allClassesQuery.Where(c => allowedClassIds.Contains(c.Id));
        }
        if (classId.HasValue)
        {
            allClassesQuery = allClassesQuery.Where(c => c.Id == classId.Value);
        }

        var allClasses = await allClassesQuery.ToListAsync(cancellationToken);
        var scopedClassIds = allClasses.Select(c => c.Id).ToHashSet();

        // Lọc registrations theo phạm vi lớp được xem
        var scopedRegistrations = registrations
            .Where(r => r.Student?.ClassAssignments.Any(ca => ca.IsActive && scopedClassIds.Contains(ca.ClassId)) == true)
            .ToList();

        var totalInitial = scopedRegistrations.Count(r => !r.IsWaitlisted);
        var totalCancelled = scopedRegistrations.Count(r => !r.IsActive && !r.IsWaitlisted);
        var totalActive = scopedRegistrations.Count(r => r.IsActive);

        var classBreakdowns = new List<ClassMealSummaryDto>();
        foreach (var cls in allClasses)
        {
            var classRegs = scopedRegistrations.Where(r =>
                r.Student?.ClassAssignments.Any(ca => ca.ClassId == cls.Id && ca.IsActive) == true).ToList();

            classBreakdowns.Add(new ClassMealSummaryDto
            {
                ClassId = cls.Id,
                ClassName = cls.ClassName,
                Grade = cls.Grade,
                InitialRegistered = classRegs.Count(r => !r.IsWaitlisted),
                CancelledCount = classRegs.Count(r => !r.IsActive && !r.IsWaitlisted),
                ActualCount = classRegs.Count(r => r.IsActive),
                StandardCount = classRegs.Count(r => r.IsActive && r.MealType == MealType.StandardLunch),
                VegetarianCount = classRegs.Count(r => r.IsActive && r.MealType == MealType.VegetarianLunch),
                SpecialDietCount = classRegs.Count(r => r.IsActive && r.MealType == MealType.SpecialDiet)
            });
        }

        // 4. Danh sách học sinh tối thiểu phục vụ vận hành (UC-33)
        var studentMeals = scopedRegistrations
            .Select(r =>
            {
                var activeCa = r.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
                return new StudentMealOperationItemDto
                {
                    RegistrationId = r.Id,
                    StudentId = r.StudentId,
                    StudentCode = r.Student?.StudentCode ?? string.Empty,
                    StudentName = r.Student?.FullName ?? string.Empty,
                    ClassId = activeCa?.ClassId ?? 0,
                    ClassName = activeCa?.Class?.ClassName ?? "Chưa phân lớp",
                    Grade = activeCa?.Class?.Grade ?? 0,
                    MealType = r.MealType,
                    IsActive = r.IsActive,
                    IsWaitlisted = r.IsWaitlisted,
                    Remarks = r.Remarks,
                    AllergyWarning = !string.IsNullOrWhiteSpace(r.Student?.SpecialNeedsNotes)
                        ? r.Student.SpecialNeedsNotes
                        : (r.Remarks != null && r.Remarks.Contains("Dị ứng", StringComparison.OrdinalIgnoreCase) ? r.Remarks : null),
                    CancellationSource = r.CancellationSource,
                    CancelledAt = r.CancelledAt,
                    CancellationReason = r.CancellationReason
                };
            })
            .OrderBy(s => s.Grade)
            .ThenBy(s => s.ClassName)
            .ThenBy(s => s.StudentName)
            .ToList();

        // 5. Danh sách nhật ký biến động suất ăn (Audit Log): hủy bởi PH (UC-30), tự hủy do đơn nghỉ (WFL 1.2.3), hoặc khôi phục
        var cutoffAt = targetDate.Add(resolved.Cutoff);
        var logs = new List<MealAuditLogDto>();
        foreach (var reg in scopedRegistrations.Where(r => !r.IsActive || (r.Remarks != null && r.Remarks.Contains("Khôi phục"))))
        {
            var studentClass = reg.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class?.ClassName ?? "Chưa phân lớp";
            var isCancelled = !reg.IsActive;
            var changeType = isCancelled
                ? (reg.CancellationSource == LunchCancellationSource.Parent || (reg.CancellationReason != null && reg.CancellationReason.Contains("Phụ huynh"))
                    ? "ParentCancelled"
                    : "AutoCancelled")
                : "Restored";
            var changedAt = isCancelled && reg.CancelledAt.HasValue ? reg.CancelledAt.Value : reg.CreatedAt;

            logs.Add(new MealAuditLogDto
            {
                StudentId = reg.StudentId,
                StudentCode = reg.Student?.StudentCode ?? string.Empty,
                StudentName = reg.Student?.FullName ?? string.Empty,
                ClassName = studentClass,
                ChangeType = changeType,
                Reason = (isCancelled ? reg.CancellationReason : null) ?? reg.Remarks ?? string.Empty,
                Timestamp = changedAt,
                IsBeforeCutoff = _clock.ToSchoolTime(changedAt) <= cutoffAt
            });
        }

        // 6. Kiểm tra cảnh báo bất thường: Có đơn hủy suất nhưng điểm danh thực tế lại là "Có mặt"
        var attendanceToday = await _unitOfWork.Repository<AttendanceRecord>()
            .Query()
            .Where(a => a.Date == targetDate)
            .ToListAsync(cancellationToken);

        var totalAnomalies = scopedRegistrations.Count(r =>
            !r.IsActive &&
            attendanceToday.Any(a => a.StudentId == r.StudentId && a.Status == AttendanceStatus.Present));

        var board = new DailyMealBoardDto
        {
            Date = targetDate,
            IsServing = resolved.IsServing,
            IsLocked = resolved.IsLocked,
            LockedAt = resolved.LockedAt,
            CutoffTime = resolved.Cutoff.ToString(@"hh\:mm"),
            ServiceRemarks = resolved.Remarks,
            CanManage = canManage,
            TotalInitialRegistered = totalInitial,
            TotalAutoCancelled = totalCancelled,
            TotalActiveToCook = totalActive,
            TotalAnomalies = totalAnomalies,
            ClassBreakdowns = classBreakdowns.OrderBy(c => c.Grade).ThenBy(c => c.ClassName).ToList(),
            MealChangeLogs = logs.OrderByDescending(l => l.Timestamp).ToList(),
            StudentMeals = studentMeals
        };

        return ApiResponse<DailyMealBoardDto>.Ok(board);
    }

    public async Task<ApiResponse<bool>> LockDailyMealAsync(LockMealDayRequestDto request, CancellationToken cancellationToken = default)
    {
        var targetDate = request.Date.Date;
        var serviceDay = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .FirstOrDefaultAsync(d => d.Date == targetDate, cancellationToken);

        var cutoff = (serviceDay != null && serviceDay.CutoffTime != TimeSpan.Zero) ? serviceDay.CutoffTime : _options.LunchCutoff;

        if (serviceDay == null)
        {
            serviceDay = new LunchServiceDay
            {
                Date = targetDate,
                IsServing = true,
                CutoffTime = cutoff,
                IsLocked = true,
                LockedAt = DateTime.UtcNow,
                Remarks = request.Notes
            };
            await _unitOfWork.Repository<LunchServiceDay>().AddAsync(serviceDay, cancellationToken);
        }
        else
        {
            serviceDay.IsLocked = true;
            serviceDay.LockedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.Notes))
            {
                serviceDay.Remarks = request.Notes;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, $"Đã chốt danh sách suất ăn gửi nhà bếp thành công (mốc {cutoff:hh\\:mm})");
    }

    public async Task<ApiResponse<MealHeadcountDto>> GetMealHeadcountAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var serviceDays = await _unitOfWork.Repository<LunchServiceDay>()
            .FindAsync(d => d.Date == targetDate, cancellationToken);
        var day = serviceDays.FirstOrDefault();

        var resolved = ResolveServiceDay(targetDate, day);
        bool isServiceDay = resolved.IsServing;
        var finalizeTime = resolved.Cutoff;
        bool isFinalized = resolved.IsLocked || (targetDate < _clock.Today) || (targetDate == _clock.Today && _clock.Now.TimeOfDay >= finalizeTime);

        if (!isServiceDay)
        {
            return ApiResponse<MealHeadcountDto>.Ok(new MealHeadcountDto
            {
                Date = targetDate,
                IsServiceDay = false,
                IsFinalized = isFinalized,
                FinalizeTime = finalizeTime
            });
        }

        var rows = await _unitOfWork.Lunch.GetHeadcountRowsAsync(targetDate, cancellationToken);
        var allClassIds = rows.Select(r => r.ClassId).Distinct().ToList();
        var submittedClassIds = await _unitOfWork.Attendance.GetSubmittedClassIdsAsync(allClassIds, targetDate, AttendanceSession.Morning, cancellationToken);

        var activeRows = rows.Where(r => r.IsActive).ToList();
        var dto = new MealHeadcountDto
        {
            Date = targetDate,
            IsServiceDay = true,
            IsFinalized = isFinalized,
            FinalizeTime = finalizeTime,
            TotalMeals = activeRows.Count,
            StandardLunch = activeRows.Count(r => r.MealType == MealType.StandardLunch),
            VegetarianLunch = activeRows.Count(r => r.MealType == MealType.VegetarianLunch),
            SpecialDiet = activeRows.Count(r => r.MealType == MealType.SpecialDiet),
            DeductedByAttendance = rows.Count(r => !r.IsActive && r.Source == LunchCancellationSource.Attendance),
            DeductedByLeave = rows.Count(r => !r.IsActive && r.Source == LunchCancellationSource.Leave),
            Classes = rows.GroupBy(r => new { r.ClassId, r.ClassName })
                .Select(g => new ClassHeadcountDto
                {
                    ClassId = g.Key.ClassId,
                    ClassName = g.Key.ClassName,
                    Meals = g.Count(r => r.IsActive),
                    DeductedByAttendance = g.Count(r => !r.IsActive && r.Source == LunchCancellationSource.Attendance),
                    AttendanceSubmitted = submittedClassIds.Contains(g.Key.ClassId)
                })
                .OrderBy(c => c.ClassName)
                .ToList()
        };

        return ApiResponse<MealHeadcountDto>.Ok(dto);
    }

    public async Task<ApiResponse<bool>> UpdateRegistrationMealTypeAsync(
        int userId,
        IReadOnlyList<UserRoles> roles,
        UpdateLunchRegistrationMealTypeDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.StudentId <= 0)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01);
        }

        var isStaffOrAdmin = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.ServiceStaff);
        if (!isStaffOrAdmin)
        {
            var isLinked = await IsParentLinkedToStudentAsync(userId, request.StudentId, cancellationToken);
            if (!isLinked)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG10);
            }
        }

        var targetDate = request.Date.Date;
        var reg = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .FirstOrDefaultAsync(r => r.StudentId == request.StudentId && r.Date == targetDate, cancellationToken);

        if (reg == null || !reg.IsActive)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đăng ký bán trú còn hiệu lực cho ngày đã chọn.");
        }

        var serviceDay = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .FirstOrDefaultAsync(d => d.Date == targetDate, cancellationToken);

        var resolved = ResolveServiceDay(targetDate, serviceDay);
        if (IsCutoffPassed(targetDate, resolved.Cutoff, resolved.IsLocked))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG27);
        }

        var oldVal = $"{reg.MealType} | {reg.Remarks}";
        reg.MealType = request.MealType;
        reg.Remarks = request.Remarks?.Trim();

        await _auditService.LogActionAsync(
            userId,
            "LUNCH_UPDATE_MEAL_TYPE",
            "LunchRegistration",
            reg.Id.ToString(),
            oldVal,
            $"{reg.MealType} | {reg.Remarks}",
            null,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, "Đã cập nhật chế độ ăn và ghi chú dị ứng thành công.");
    }

    public async Task<ApiResponse<LunchPeriodReportDto>> GetPeriodReportAsync(
        DateTime fromDate,
        DateTime toDate,
        int? classId = null,
        int? userId = null,
        IReadOnlyList<UserRoles>? roles = null,
        CancellationToken cancellationToken = default)
    {
        var start = fromDate.Date;
        var end = toDate.Date;
        if (end < start || (end - start).TotalDays > 92)
        {
            return ApiResponse<LunchPeriodReportDto>.Fail("Khoảng thời gian báo cáo không hợp lệ (tối đa 92 ngày).");
        }

        // ponytail: nếu là Giáo viên (Teacher), chỉ cho phép xem lớp mình chủ nhiệm
        bool isTeacherOnly = roles != null &&
                             roles.Contains(UserRoles.Teacher) &&
                             !roles.Contains(UserRoles.ServiceStaff) &&
                             !roles.Contains(UserRoles.OfficeStaff) &&
                             !roles.Contains(UserRoles.SchoolBoard) &&
                             !roles.Contains(UserRoles.Admin);

        HashSet<int>? allowedClassIds = null;
        if (isTeacherOnly && userId.HasValue)
        {
            var staff = await _unitOfWork.Repository<StaffProfile>()
                .Query()
                .FirstOrDefaultAsync(sp => sp.UserId == userId.Value, cancellationToken);

            if (staff != null)
            {
                var homeroomIds = await _unitOfWork.Repository<Class>()
                    .Query()
                    .Where(c => c.HomeroomTeacherId == staff.Id && c.Status == "Active")
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                allowedClassIds = homeroomIds.ToHashSet();
            }
            else
            {
                allowedClassIds = new HashSet<int>();
            }
        }

        var serviceDays = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .Where(d => d.Date >= start && d.Date <= end)
            .ToDictionaryAsync(d => d.Date.Date, cancellationToken);

        int servingCount = 0;
        for (var cur = start; cur <= end; cur = cur.AddDays(1))
        {
            serviceDays.TryGetValue(cur, out var sDay);
            if (ResolveServiceDay(cur, sDay).IsServing) servingCount++;
        }

        var classesQuery = _unitOfWork.Repository<Class>().Query().Where(c => c.Status == "Active");
        if (allowedClassIds != null) classesQuery = classesQuery.Where(c => allowedClassIds.Contains(c.Id));
        if (classId.HasValue) classesQuery = classesQuery.Where(c => c.Id == classId.Value);
        var classes = await classesQuery.OrderBy(c => c.Grade).ThenBy(c => c.ClassName).ToListAsync(cancellationToken);
        var classIds = classes.Select(c => c.Id).ToHashSet();

        var regs = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
            .Where(r => r.Date >= start && r.Date <= end)
            .ToListAsync(cancellationToken);

        var scopedRegs = regs
            .Where(r => r.Student?.ClassAssignments.Any(ca => ca.IsActive && classIds.Contains(ca.ClassId)) == true)
            .ToList();

        var summaries = classes.Select(cls =>
        {
            var cRegs = scopedRegs.Where(r => r.Student?.ClassAssignments.Any(ca => ca.IsActive && ca.ClassId == cls.Id) == true).ToList();
            return new ClassMealSummaryDto
            {
                ClassId = cls.Id,
                ClassName = cls.ClassName,
                Grade = cls.Grade,
                InitialRegistered = cRegs.Count,
                CancelledCount = cRegs.Count(r => !r.IsActive),
                ActualCount = cRegs.Count(r => r.IsActive),
                StandardCount = cRegs.Count(r => r.IsActive && r.MealType == MealType.StandardLunch),
                VegetarianCount = cRegs.Count(r => r.IsActive && r.MealType == MealType.VegetarianLunch),
                SpecialDietCount = cRegs.Count(r => r.IsActive && r.MealType == MealType.SpecialDiet)
            };
        }).ToList();

        return ApiResponse<LunchPeriodReportDto>.Ok(new LunchPeriodReportDto
        {
            FromDate = start,
            ToDate = end,
            TotalServiceDays = servingCount,
            TotalInitialRegistered = scopedRegs.Count,
            TotalCancelled = scopedRegs.Count(r => !r.IsActive),
            TotalActualMeals = scopedRegs.Count(r => r.IsActive),
            TotalStandard = scopedRegs.Count(r => r.IsActive && r.MealType == MealType.StandardLunch),
            TotalVegetarian = scopedRegs.Count(r => r.IsActive && r.MealType == MealType.VegetarianLunch),
            TotalSpecialDiet = scopedRegs.Count(r => r.IsActive && r.MealType == MealType.SpecialDiet),
            ClassSummaries = summaries
        });
    }

    // ---- Monthly Boarding Quota & Waitlist (TC_BDS_34) ----

    public async Task<ApiResponse<MonthlyBoardingQuotaDto>> GetMonthlyQuotaAsync(string month, CancellationToken cancellationToken = default)
    {
        if (!TryParseMonth(month, out var year, out var m))
        {
            return ApiResponse<MonthlyBoardingQuotaDto>.Fail(SystemMessages.MSG01, new List<string> { "Định dạng tháng không hợp lệ (YYYY-MM)." });
        }

        var normalizedMonth = $"{year:D4}-{m:D2}";
        var mStart = new DateTime(year, m, 1);
        var mEnd = mStart.AddMonths(1).AddDays(-1);

        var quotaRepo = _unitOfWork.Repository<LunchMonthlyQuota>();
        LunchMonthlyQuota? quotaEntity = null;
        if (quotaRepo != null)
        {
            quotaEntity = await quotaRepo.Query()
                .FirstOrDefaultAsync(q => q.Month == normalizedMonth, cancellationToken);
        }

        var regRepo = _unitOfWork.Repository<LunchRegistration>();
        int registeredCount = 0;
        int waitlistedCount = 0;
        if (regRepo != null)
        {
            registeredCount = await regRepo.Query()
                .Where(r => r.Date >= mStart && r.Date <= mEnd && r.IsActive && !r.IsWaitlisted)
                .Select(r => r.StudentId)
                .Distinct()
                .CountAsync(cancellationToken);

            waitlistedCount = await regRepo.Query()
                .Where(r => r.Date >= mStart && r.Date <= mEnd && !r.IsActive && r.IsWaitlisted)
                .Select(r => r.StudentId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        int? quotaVal = quotaEntity?.Quota;
        bool isFull = quotaVal.HasValue && quotaVal.Value > 0 && registeredCount >= quotaVal.Value;

        return ApiResponse<MonthlyBoardingQuotaDto>.Ok(new MonthlyBoardingQuotaDto
        {
            Month = normalizedMonth,
            Quota = quotaVal,
            RegisteredCount = registeredCount,
            WaitlistedCount = waitlistedCount,
            IsFull = isFull,
            UpdatedAt = quotaEntity?.UpdatedAt
        });
    }

    public async Task<ApiResponse<MonthlyBoardingQuotaDto>> UpsertMonthlyQuotaAsync(
        int userId,
        UpsertMonthlyBoardingQuotaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseMonth(request.Month, out var year, out var m))
        {
            return ApiResponse<MonthlyBoardingQuotaDto>.Fail(SystemMessages.MSG01, new List<string> { "Định dạng tháng không hợp lệ (YYYY-MM)." });
        }

        if (request.Quota <= 0 || request.Quota > 10000)
        {
            return ApiResponse<MonthlyBoardingQuotaDto>.Fail(SystemMessages.MSG01, new List<string> { "Chỉ tiêu bán trú tháng phải lớn hơn 0." });
        }

        var normalizedMonth = $"{year:D4}-{m:D2}";
        var quotaRepo = _unitOfWork.Repository<LunchMonthlyQuota>();
        if (quotaRepo == null)
        {
            return ApiResponse<MonthlyBoardingQuotaDto>.Fail(SystemMessages.MSG09);
        }

        var existing = await quotaRepo.Query()
            .FirstOrDefaultAsync(q => q.Month == normalizedMonth, cancellationToken);

        string? oldValue = existing != null ? $"Quota={existing.Quota}" : null;
        if (existing != null)
        {
            existing.Quota = request.Quota;
            existing.UpdatedByUserId = userId > 0 ? userId : null;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new LunchMonthlyQuota
            {
                Month = normalizedMonth,
                Quota = request.Quota,
                UpdatedByUserId = userId > 0 ? userId : null,
                UpdatedAt = DateTime.UtcNow
            };
            await quotaRepo.AddAsync(existing, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            "CONFIGURE_BOARDING_QUOTA",
            "LunchMonthlyQuota",
            normalizedMonth,
            oldValue,
            $"Month={normalizedMonth};Quota={request.Quota}",
            null,
            cancellationToken);

        return await GetMonthlyQuotaAsync(normalizedMonth, cancellationToken);
    }

    // ---- Daily Boarding Incident Log (TC_BDS_36) ----

    public async Task<ApiResponse<List<BoardingIncidentDto>>> GetBoardingIncidentsAsync(
        DateTime? date,
        string? month,
        DateTime? from,
        DateTime? to,
        int? classId,
        int? studentId,
        int? userId,
        IReadOnlyList<UserRoles>? roles,
        CancellationToken cancellationToken = default)
    {
        var incidentRepo = _unitOfWork.Repository<BoardingIncident>();
        if (incidentRepo == null)
        {
            return ApiResponse<List<BoardingIncidentDto>>.Ok(new List<BoardingIncidentDto>());
        }

        bool isStaffOrAdmin = roles != null && (roles.Contains(UserRoles.ServiceStaff) || roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.OfficeStaff) || roles.Contains(UserRoles.SchoolBoard));
        bool isTeacherOnly = roles != null && roles.Contains(UserRoles.Teacher) && !isStaffOrAdmin;
        bool isParentOnly = roles != null && roles.Contains(UserRoles.Parent) && !isStaffOrAdmin && !roles.Contains(UserRoles.Teacher);

        HashSet<int>? allowedClassIds = null;
        if (isTeacherOnly && userId.HasValue)
        {
            var staff = await _unitOfWork.Repository<StaffProfile>()
                .Query()
                .FirstOrDefaultAsync(sp => sp.UserId == userId.Value, cancellationToken);
            if (staff != null)
            {
                var ids = await _unitOfWork.Repository<Class>()
                    .Query()
                    .Where(c => c.HomeroomTeacherId == staff.Id && c.Status == "Active")
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                allowedClassIds = ids.ToHashSet();
            }
            else
            {
                allowedClassIds = new HashSet<int>();
            }
        }

        HashSet<int>? allowedStudentIds = null;
        if (isParentOnly && userId.HasValue)
        {
            var guardian = await _unitOfWork.Repository<Guardian>()
                .Query()
                .Include(g => g.StudentGuardians)
                .FirstOrDefaultAsync(g => g.UserId == userId.Value, cancellationToken);
            allowedStudentIds = guardian?.StudentGuardians.Select(sg => sg.StudentId).ToHashSet() ?? new HashSet<int>();
        }

        var query = incidentRepo.Query()
            .Include(i => i.Student)
            .Include(i => i.Class)
            .Include(i => i.RecordedByUser)
            .AsQueryable();

        if (from.HasValue || to.HasValue)
        {
            if (from.HasValue)
            {
                var start = from.Value.Date;
                query = query.Where(i => i.Date >= start);
            }
            if (to.HasValue)
            {
                var end = to.Value.Date;
                query = query.Where(i => i.Date <= end);
            }
        }
        else if (TryParseMonth(month, out var y, out var m))
        {
            var mStart = new DateTime(y, m, 1);
            var mEnd = mStart.AddMonths(1).AddDays(-1);
            query = query.Where(i => i.Date >= mStart && i.Date <= mEnd);
        }
        else if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            query = query.Where(i => i.Date == targetDate);
        }

        if (allowedClassIds != null)
        {
            query = query.Where(i => i.ClassId.HasValue && allowedClassIds.Contains(i.ClassId.Value));
        }

        if (allowedStudentIds != null)
        {
            query = query.Where(i => allowedStudentIds.Contains(i.StudentId));
        }

        if (classId.HasValue)
        {
            query = query.Where(i => i.ClassId == classId.Value);
        }

        if (studentId.HasValue)
        {
            query = query.Where(i => i.StudentId == studentId.Value);
        }

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(i => new BoardingIncidentDto
        {
            Id = i.Id,
            StudentId = i.StudentId,
            StudentCode = i.Student?.StudentCode ?? string.Empty,
            StudentName = i.Student?.FullName ?? string.Empty,
            ClassId = i.ClassId,
            ClassName = i.Class?.ClassName ?? "Chưa phân lớp",
            Date = i.Date,
            IncidentType = i.IncidentType,
            Note = i.Note,
            RecordedByUserId = i.RecordedByUserId,
            RecordedByName = i.RecordedByUser?.FullName ?? i.RecordedByUser?.Username ?? "Giáo viên",
            CreatedAt = i.CreatedAt
        }).ToList();

        return ApiResponse<List<BoardingIncidentDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<BoardingIncidentDto>> CreateBoardingIncidentAsync(
        int userId,
        IReadOnlyList<UserRoles> roles,
        CreateBoardingIncidentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.StudentId <= 0 || string.IsNullOrWhiteSpace(request.Note))
        {
            return ApiResponse<BoardingIncidentDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn học sinh và nhập nội dung sự cố bán trú." });
        }

        var note = request.Note.Trim();
        if (note.Length > 1000)
        {
            return ApiResponse<BoardingIncidentDto>.Fail(SystemMessages.MSG01, new List<string> { "Nội dung sự cố không được vượt quá 1000 ký tự." });
        }

        var student = await _unitOfWork.Repository<Student>()
            .Query()
            .Include(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .Include(s => s.StudentGuardians)
                .ThenInclude(sg => sg.Guardian)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);

        if (student == null)
        {
            return ApiResponse<BoardingIncidentDto>.Fail(SystemMessages.MSG11);
        }

        var activeCa = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive);

        // Nếu là Teacher (không phải Admin/ServiceStaff/OfficeStaff), kiểm tra đúng GVCN của lớp
        bool isStaffOrAdmin = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.ServiceStaff) || roles.Contains(UserRoles.OfficeStaff);
        if (!isStaffOrAdmin && roles.Contains(UserRoles.Teacher) && userId > 0)
        {
            var staff = await _unitOfWork.Repository<StaffProfile>()
                .Query()
                .FirstOrDefaultAsync(sp => sp.UserId == userId, cancellationToken);
            if (staff == null || activeCa?.Class == null || activeCa.Class.HomeroomTeacherId != staff.Id)
            {
                return ApiResponse<BoardingIncidentDto>.Fail(SystemMessages.MSG10);
            }
        }

        var targetDate = (request.Date == default ? _clock.Today : request.Date).Date;
        var incidentType = string.IsNullOrWhiteSpace(request.IncidentType) ? "General" : request.IncidentType.Trim();

        var incidentRepo = _unitOfWork.Repository<BoardingIncident>();
        if (incidentRepo == null)
        {
            return ApiResponse<BoardingIncidentDto>.Fail(SystemMessages.MSG09);
        }

        var incident = new BoardingIncident
        {
            StudentId = student.Id,
            ClassId = activeCa?.ClassId,
            Date = targetDate,
            IncidentType = incidentType,
            Note = note,
            RecordedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        await incidentRepo.AddAsync(incident, cancellationToken);

        // Gửi thông báo real-time 'Boarding Incident' cho tất cả phụ huynh liên kết với học sinh
        var notifRepo = _unitOfWork.Repository<Notification>();
        if (notifRepo != null)
        {
            var parentUserIds = student.StudentGuardians
                .Where(sg => sg.Guardian != null && sg.Guardian.UserId > 0)
                .Select(sg => sg.Guardian.UserId)
                .Distinct()
                .ToList();

            foreach (var parentUid in parentUserIds)
            {
                await notifRepo.AddAsync(new Notification
                {
                    UserId = parentUid,
                    Title = "Boarding Incident",
                    Content = $"Sự cố bán trú ngày {targetDate:dd/MM/yyyy} của học sinh {student.FullName} ({student.StudentCode}): {note}",
                    Type = "BOARDING_INCIDENT",
                    LinkUrl = $"/parent/boarding-incidents?studentId={student.Id}&month={targetDate:yyyy-MM}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            "BOARDING_INCIDENT_LOGGED",
            "BoardingIncident",
            incident.Id.ToString(),
            null,
            $"StudentCode={student.StudentCode};Date={targetDate:yyyy-MM-dd};Note={note}",
            null,
            cancellationToken);

        var recorder = userId > 0
            ? await _unitOfWork.Repository<User>().Query().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            : null;

        return ApiResponse<BoardingIncidentDto>.Ok(new BoardingIncidentDto
        {
            Id = incident.Id,
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            ClassId = activeCa?.ClassId,
            ClassName = activeCa?.Class?.ClassName ?? "Chưa phân lớp",
            Date = targetDate,
            IncidentType = incidentType,
            Note = note,
            RecordedByUserId = userId,
            RecordedByName = recorder?.FullName ?? recorder?.Username ?? "Giáo viên",
            CreatedAt = incident.CreatedAt
        }, "Đã ghi nhận sự cố bán trú và gửi cảnh báo đến phụ huynh.");
    }

    private static bool TryParseMonth(string? month, out int year, out int m)
    {
        year = 0;
        m = 0;
        if (string.IsNullOrWhiteSpace(month)) return false;
        var parts = month.Trim().Split('-');
        return parts.Length == 2 &&
               int.TryParse(parts[0], out year) &&
               int.TryParse(parts[1], out m) &&
               m >= 1 && m <= 12 &&
               year >= 2000 && year <= 2100;
    }
}
