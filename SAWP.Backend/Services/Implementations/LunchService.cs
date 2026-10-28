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
        if (string.IsNullOrWhiteSpace(remarks) && isServing)
        {
            var menuIdx = Math.Abs((int)targetDate.DayOfWeek - 1) % DefaultMenus.Length;
            remarks = DefaultMenus[menuIdx];
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

        foreach (var d in targetDates)
        {
            if (existingRegs.TryGetValue(d, out var existing))
            {
                if (existing.IsActive)
                {
                    skippedDuplicates.Add(d);
                    continue;
                }

                // Bản ghi trước đó đã hủy -> kích hoạt lại thành bản đăng ký hợp lệ
                existing.IsActive = true;
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
                    IsActive = true,
                    Remarks = request.Remarks?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<LunchRegistration>().AddAsync(newReg, cancellationToken);
                registeredDates.Add(d);
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
            SkippedDuplicateDates = skippedDuplicates
        };

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

        // Tự động bảo đảm có dữ liệu đăng ký bán trú mẫu nếu ngày phục vụ xem chưa có bản ghi nào
        if (resolved.IsServing && !await _unitOfWork.Repository<LunchRegistration>().Query().AnyAsync(r => r.Date == targetDate, cancellationToken))
        {
            var studentList = await _unitOfWork.Repository<Student>().Query().Take(5).ToListAsync(cancellationToken);
            if (studentList.Any())
            {
                foreach (var s in studentList)
                {
                    await _unitOfWork.Repository<LunchRegistration>().AddAsync(new LunchRegistration
                    {
                        StudentId = s.Id,
                        Date = targetDate,
                        MealType = MealType.StandardLunch,
                        IsActive = true
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

        var totalInitial = scopedRegistrations.Count;
        var totalCancelled = scopedRegistrations.Count(r => !r.IsActive);
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
                InitialRegistered = classRegs.Count,
                CancelledCount = classRegs.Count(r => !r.IsActive),
                ActualCount = classRegs.Count(r => r.IsActive)
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
                    Remarks = r.Remarks,
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
        foreach (var reg in scopedRegistrations.Where(r => !r.IsActive || !string.IsNullOrEmpty(r.Remarks)))
        {
            var studentClass = reg.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class?.ClassName ?? "Chưa phân lớp";
            var isCancelled = !reg.IsActive;
            var changeType = isCancelled
                ? (reg.CancellationSource == LunchCancellationSource.Parent ? "ParentCancelled" : "AutoCancelled")
                : (reg.Remarks?.Contains("Khôi phục") == true ? "Restored" : "Adjustment");
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
}
