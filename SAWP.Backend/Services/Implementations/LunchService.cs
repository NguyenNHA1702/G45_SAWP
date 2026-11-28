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

public class LunchService : ILunchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISchoolClock _clock;
    private readonly SchoolOptions _options;
    private readonly IAuditService _auditService;
    private readonly ISchoolCalendarService _calendarService;

    private static readonly TimeSpan MinCutoffTime = new(6, 0, 0);
    private static readonly TimeSpan MaxCutoffTime = new(11, 0, 0);
    private const int MaxRemarksLength = 250;
    private const int MaxCancelReasonLength = 300;

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
        IAuditService auditService,
        ISchoolCalendarService? calendarService = null)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options.Value;
        _auditService = auditService;
        _calendarService = calendarService ?? new SchoolCalendarService(unitOfWork);
    }

    private async Task<Dictionary<DateTime, bool>> GetCalendarDayMapAsync(DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        var repo = _unitOfWork.Repository<SchoolCalendarDay>();
        if (repo == null) return new Dictionary<DateTime, bool>();

        var rows = await repo.Query()
            .Where(d => d.Date >= startDate.Date && d.Date <= endDate.Date)
            .ToListAsync(ct);

        return rows
            .GroupBy(d => d.Date.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().IsSchoolDay);
    }

    private async Task<bool?> GetCalendarSchoolDayFlagAsync(DateTime date, CancellationToken ct)
    {
        var repo = _unitOfWork.Repository<SchoolCalendarDay>();
        if (repo == null) return null;

        var row = await repo.Query()
            .Where(d => d.Date == date.Date)
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync(ct);

        return row?.IsSchoolDay;
    }

    private async Task<Dictionary<DateTime, List<LunchServiceDayDish>>> GetServiceDayDishesMapAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct)
    {
        var dayDishRepo = _unitOfWork.Repository<LunchServiceDayDish>();
        if (dayDishRepo == null) return new Dictionary<DateTime, List<LunchServiceDayDish>>();

        var rows = await dayDishRepo.Query()
            .Include(sdd => sdd.LunchServiceDay)
            .Include(sdd => sdd.Dish)
            .Where(sdd => sdd.LunchServiceDay != null
                          && sdd.LunchServiceDay.Date >= startDate.Date
                          && sdd.LunchServiceDay.Date <= endDate.Date)
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.LunchServiceDay.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private async Task<Dictionary<int, StudentMealProfile>> GetStudentMealProfileMapAsync(
        IEnumerable<int> studentIds,
        CancellationToken ct)
    {
        var profileRepo = _unitOfWork.Repository<StudentMealProfile>();
        if (profileRepo == null) return new Dictionary<int, StudentMealProfile>();

        var ids = studentIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, StudentMealProfile>();

        var rows = await profileRepo.Query()
            .Where(p => ids.Contains(p.StudentId))
            .ToListAsync(ct);

        return rows.ToDictionary(p => p.StudentId);
    }

    private static List<Dish> SelectDishesForMealType(List<LunchServiceDayDish>? dayDishes, MealType mealType)
    {
        if (dayDishes == null || dayDishes.Count == 0) return new List<Dish>();
        var exact = dayDishes
            .Where(d => d.MealType == mealType && d.Dish != null)
            .Select(d => d.Dish)
            .ToList();
        if (exact.Count > 0) return exact;

        return dayDishes
            .Where(d => d.MealType == MealType.StandardLunch && d.Dish != null)
            .Select(d => d.Dish)
            .ToList();
    }

    private (bool IsServing, TimeSpan Cutoff, bool IsLocked, DateTime? LockedAt, string? Remarks) ResolveServiceDay(DateTime date, LunchServiceDay? day, bool? isCalendarSchoolDay = null)
    {
        var targetDate = date.Date;
        // Nếu SchoolCalendarDay có cấu hình rõ ràng (VD: học bù T7 IsSchoolDay=true, hoặc nghỉ lễ 02/12 IsSchoolDay=false) thì ưu tiên theo lịch nhà trường
        var defaultServing = isCalendarSchoolDay ?? SchoolCalendarHelper.IsDefaultSchoolDay(targetDate);
        var isServing = isCalendarSchoolDay == false ? false : (day != null ? day.IsServing : defaultServing);
        var cutoff = (day != null && day.CutoffTime != TimeSpan.Zero) ? day.CutoffTime : _options.LunchCutoff;
        var isLocked = day?.IsLocked ?? false;
        var lockedAt = UtcDateTimeHelper.Normalize(day?.LockedAt);

        string? remarks = day?.Remarks;
        if ((string.IsNullOrWhiteSpace(remarks) || remarks == "Phục vụ bán trú bình thường") && isServing)
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
        return _clock.Now >= cutoffMoment;
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
        var calendarMap = await GetCalendarDayMapAsync(start, end, cancellationToken);

        var calendarSchoolDays = await _calendarService.GetSchoolDaysInRangeAsync(start, end, cancellationToken);
        var dayDishesMap = await GetServiceDayDishesMapAsync(start, end, cancellationToken);

        var result = new List<LunchServiceDayConfigDto>();
        for (var cur = start; cur <= end; cur = cur.AddDays(1))
        {
            existingDays.TryGetValue(cur, out var day);
            bool? isCalSchoolDay = calendarMap.TryGetValue(cur, out var calFlag) ? calFlag : null;
            var resolved = ResolveServiceDay(cur, day, isCalSchoolDay);
            dayDishesMap.TryGetValue(cur, out var dayDishes);
            var menusDto = LunchMenuService.BuildDailyMenusDto(dayDishes);
            var effectiveRemarks = resolved.Remarks;
            if (menusDto != null)
            {
                var rawDayRemarks = day?.Remarks;
                effectiveRemarks = (!string.IsNullOrWhiteSpace(rawDayRemarks) && rawDayRemarks != "Phục vụ bán trú bình thường")
                    ? rawDayRemarks
                    : (LunchMenuService.BuildSummaryRemarks(menusDto) ?? resolved.Remarks);
            }
            result.Add(new LunchServiceDayConfigDto
            {
                Date = cur,
                IsServing = resolved.IsServing,
                CutoffTime = resolved.Cutoff.ToString(@"hh\:mm"),
                IsLocked = resolved.IsLocked,
                LockedAt = resolved.LockedAt,
                Remarks = effectiveRemarks,
                Menus = menusDto
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

        if (!TimeSpan.TryParse(request.CutoffTime, out var parsedCutoff) || parsedCutoff < MinCutoffTime || parsedCutoff > MaxCutoffTime)
        {
            return ApiResponse<List<LunchServiceDayConfigDto>>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Giờ chốt suất ăn (CutoffTime) phải có định dạng HH:mm trong khoảng 06:00 đến 11:00 (trước giờ ăn trưa)." });
        }

        var normalizedRemarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        if (normalizedRemarks != null && normalizedRemarks.Length > MaxRemarksLength)
        {
            return ApiResponse<List<LunchServiceDayConfigDto>>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Ghi chú / thực đơn không được vượt quá {MaxRemarksLength} ký tự." });
        }

        var targetDates = request.Dates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        var existingMap = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .Include(d => d.Dishes)
                .ThenInclude(sdd => sdd.Dish)
            .Where(d => targetDates.Contains(d.Date))
            .ToDictionaryAsync(d => d.Date.Date, cancellationToken);

        WeeklyMenuTemplate? selectedTemplate = null;
        if (request.MenuTemplateId.HasValue && request.IsServing)
        {
            var tplRepo = _unitOfWork.Repository<WeeklyMenuTemplate>();
            if (tplRepo != null)
            {
                selectedTemplate = await tplRepo.Query()
                    .Include(t => t.Items)
                        .ThenInclude(i => i.Dish)
                    .FirstOrDefaultAsync(t => t.Id == request.MenuTemplateId.Value && t.IsActive, cancellationToken);
            }
            if (selectedTemplate == null)
            {
                return ApiResponse<List<LunchServiceDayConfigDto>>.Fail(
                    SystemMessages.MSG11,
                    new List<string> { "Không tìm thấy bộ Thực đơn tuần đang hoạt động." });
            }
        }

        Dictionary<int, Dish> dishLookup = new();
        if (selectedTemplate == null && request.MenusByMealType != null)
        {
            var requestedDishIds = request.MenusByMealType.Values
                .Where(v => v != null)
                .SelectMany(v => v)
                .Distinct()
                .ToList();
            if (requestedDishIds.Count > 0)
            {
                var dishRepo = _unitOfWork.Repository<Dish>();
                if (dishRepo != null)
                {
                    dishLookup = await dishRepo.Query()
                        .Where(d => requestedDishIds.Contains(d.Id))
                        .ToDictionaryAsync(d => d.Id, cancellationToken);
                    if (dishLookup.Count != requestedDishIds.Count)
                    {
                        return ApiResponse<List<LunchServiceDayConfigDto>>.Fail(
                            SystemMessages.MSG01,
                            new List<string> { "Một hoặc nhiều món ăn được chọn không tồn tại trong thư viện." });
                    }
                }
            }
        }

        var lockedDates = targetDates
            .Where(d => existingMap.TryGetValue(d, out var ex) && ex.IsLocked)
            .ToList();
        if (lockedDates.Count > 0)
        {
            var lockedLabel = string.Join(", ", lockedDates.Select(d => d.ToString("dd/MM/yyyy")));
            return ApiResponse<List<LunchServiceDayConfigDto>>.Fail(
                SystemMessages.MSG19,
                new List<string> { $"Ngày ({lockedLabel}) đã chốt sổ bán trú. Vui lòng mở khóa trước khi thay đổi cấu hình." });
        }

        var updatedDtos = new List<LunchServiceDayConfigDto>();
        var dayDishRepo = _unitOfWork.Repository<LunchServiceDayDish>();
        var savedEntities = new List<LunchServiceDay>();
        var preserveExistingMenu = selectedTemplate == null && request.MenusByMealType == null;

        foreach (var d in targetDates)
        {
            if (existingMap.TryGetValue(d, out var existing))
            {
                existing.IsServing = request.IsServing;
                existing.CutoffTime = parsedCutoff;
                if (normalizedRemarks != null || !preserveExistingMenu || existing.Dishes.Count == 0 || !request.IsServing)
                {
                    existing.Remarks = normalizedRemarks;
                }
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
            savedEntities.Add(existing);
        }

        if (selectedTemplate != null)
        {
            foreach (var existing in savedEntities)
            {
                foreach (var oldDish in existing.Dishes.ToList())
                {
                    if (dayDishRepo != null)
                    {
                        await dayDishRepo.DeleteAsync(oldDish, cancellationToken);
                    }
                    existing.Dishes.Remove(oldDish);
                }

                var dow = existing.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)existing.Date.DayOfWeek;
                var tplItems = selectedTemplate.Items
                    .Where(i => i.DayOfWeek == dow && i.Dish != null && i.Dish.IsActive)
                    .ToList();
                if (tplItems.Count == 0 && dow >= 6)
                {
                    tplItems = selectedTemplate.Items
                        .Where(i => i.DayOfWeek == 1 && i.Dish != null && i.Dish.IsActive)
                        .ToList();
                }

                var newDayDishes = new List<LunchServiceDayDish>();
                foreach (var item in tplItems)
                {
                    var entry = new LunchServiceDayDish
                    {
                        LunchServiceDayId = existing.Id,
                        LunchServiceDay = existing,
                        MealType = item.MealType,
                        DishId = item.DishId,
                        Dish = item.Dish,
                        SortOrder = item.SortOrder
                    };
                    existing.Dishes.Add(entry);
                    newDayDishes.Add(entry);
                }

                var builtMenus = LunchMenuService.BuildDailyMenusDto(newDayDishes);
                if (string.IsNullOrWhiteSpace(normalizedRemarks))
                {
                    existing.Remarks = LunchMenuService.BuildSummaryRemarks(builtMenus);
                }
            }
        }
        else if (request.MenusByMealType != null && dayDishRepo != null)
        {
            foreach (var existing in savedEntities)
            {
                foreach (var oldDish in existing.Dishes.ToList())
                {
                    await dayDishRepo.DeleteAsync(oldDish, cancellationToken);
                    existing.Dishes.Remove(oldDish);
                }

                var newDayDishes = new List<LunchServiceDayDish>();
                foreach (var kv in request.MenusByMealType)
                {
                    if (kv.Value == null) continue;
                    int sort = 1;
                    foreach (var dishId in kv.Value.Distinct())
                    {
                        if (!dishLookup.TryGetValue(dishId, out var dishObj)) continue;
                        var entry = new LunchServiceDayDish
                        {
                            LunchServiceDayId = existing.Id,
                            LunchServiceDay = existing,
                            MealType = kv.Key,
                            DishId = dishId,
                            Dish = dishObj,
                            SortOrder = sort++
                        };
                        existing.Dishes.Add(entry);
                        newDayDishes.Add(entry);
                    }
                }

                var builtMenus = LunchMenuService.BuildDailyMenusDto(newDayDishes);
                if (string.IsNullOrWhiteSpace(normalizedRemarks))
                {
                    existing.Remarks = LunchMenuService.BuildSummaryRemarks(builtMenus);
                }
            }
        }

        foreach (var existing in savedEntities)
        {
            updatedDtos.Add(new LunchServiceDayConfigDto
            {
                Date = existing.Date,
                IsServing = existing.IsServing,
                CutoffTime = existing.CutoffTime.ToString(@"hh\:mm"),
                IsLocked = existing.IsLocked,
                LockedAt = UtcDateTimeHelper.Normalize(existing.LockedAt),
                Remarks = existing.Remarks,
                Menus = LunchMenuService.BuildDailyMenusDto(existing.Dishes)
            });
        }

        // D-D: Khi đóng ngày phục vụ (IsServing = false), tự động hủy các suất đã đăng ký và thông báo phụ huynh
        var autoCancelledCount = 0;
        if (!request.IsServing)
        {
            var regRepo = _unitOfWork.Repository<LunchRegistration>();
            if (regRepo != null)
            {
                var activeRegsToCancel = await regRepo.Query()
                    .Include(r => r.Student)
                    .Where(r => targetDates.Contains(r.Date) && r.IsActive)
                    .ToListAsync(cancellationToken);

                if (activeRegsToCancel.Count > 0)
                {
                    var utcNow = DateTime.UtcNow;
                    foreach (var reg in activeRegsToCancel)
                    {
                        var reasonBase = $"Nhà trường đóng phục vụ bán trú ngày {reg.Date:dd/MM/yyyy}";
                        var fullReason = !string.IsNullOrWhiteSpace(normalizedRemarks)
                            ? $"{reasonBase}: {normalizedRemarks}"
                            : reasonBase;
                        if (fullReason.Length > MaxCancelReasonLength)
                        {
                            fullReason = fullReason[..MaxCancelReasonLength];
                        }

                        reg.IsActive = false;
                        reg.IsWaitlisted = false;
                        reg.CancellationSource = LunchCancellationSource.Staff;
                        reg.CancelledAt = utcNow;
                        reg.CancelledByUserId = userId > 0 ? userId : null;
                        reg.CancellationReason = fullReason;
                    }
                    autoCancelledCount = activeRegsToCancel.Count;

                    var notifRepo = _unitOfWork.Repository<Notification>();
                    if (notifRepo != null && _unitOfWork.Students != null)
                    {
                        var studentIds = activeRegsToCancel.Select(r => r.StudentId).Distinct().ToList();
                        var guardianMap = await _unitOfWork.Students.GetGuardianUserIdsAsync(studentIds, cancellationToken);
                        foreach (var reg in activeRegsToCancel)
                        {
                            if (!guardianMap.TryGetValue(reg.StudentId, out var guardianUserIds)) continue;
                            foreach (var gUserId in guardianUserIds.Distinct())
                            {
                                await notifRepo.AddAsync(new Notification
                                {
                                    UserId = gUserId,
                                    Title = "Thông báo hủy suất ăn do đóng ngày phục vụ",
                                    Content = $"Suất ăn bán trú ngày {reg.Date:dd/MM/yyyy} của học sinh {reg.Student?.FullName ?? $"#{reg.StudentId}"} đã được tự động hủy do nhà trường không phục vụ bán trú ngày này.",
                                    LinkUrl = $"/parent/lunch?studentId={reg.StudentId}&month={reg.Date:yyyy-MM}",
                                    CreatedAt = utcNow,
                                    IsRead = false
                                }, cancellationToken);
                            }
                        }
                    }
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            userId,
            AuditActions.LunchServiceDayConfigure,
            nameof(LunchServiceDay),
            null,
            null,
            new { Dates = targetDates.Select(d => d.ToString("yyyy-MM-dd")).ToList(), request.IsServing, CutoffTime = parsedCutoff.ToString(@"hh\:mm"), Remarks = normalizedRemarks, AutoCancelledRegistrations = autoCancelledCount },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return autoCancelledCount > 0
            ? ApiResponse<List<LunchServiceDayConfigDto>>.Ok(updatedDtos, $"Đã cập nhật cấu hình ngày phục vụ và tự động hủy {autoCancelledCount} suất ăn đã đăng ký.")
            : ApiResponse<List<LunchServiceDayConfigDto>>.Ok(updatedDtos, SystemMessages.MSG44);
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
        var calendarMap = await GetCalendarDayMapAsync(startOfMonth, endOfMonth, cancellationToken);

        var registrations = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Where(r => r.StudentId == studentId && r.Date >= startOfMonth && r.Date <= endOfMonth)
            .ToDictionaryAsync(r => r.Date.Date, cancellationToken);

        var calendarSchoolDays = await _calendarService.GetSchoolDaysInRangeAsync(startOfMonth, endOfMonth, cancellationToken);
        var dayDishesMap = await GetServiceDayDishesMapAsync(startOfMonth, endOfMonth, cancellationToken);
        var profileMap = await GetStudentMealProfileMapAsync(new[] { studentId }, cancellationToken);
        profileMap.TryGetValue(studentId, out var studentProfile);
        var studentProfileDto = LunchMenuService.BuildProfileDto(student, studentProfile);
        var studentAllergyTags = studentProfileDto.AllergenTags;
        var trustedAllergyNotes = studentProfileDto.AllergyNotes;
        var contextualStudentNote = AllergenCatalog.ExtractExplicitAllergyNote(student.SpecialNeedsNotes);

        // BD-02: Lấy các ngày đang có đơn xin nghỉ đã duyệt (buổi Sáng / Cả ngày, không giữ suất ăn) để hiển thị rõ trên lịch bán trú phụ huynh
        var approvedLeaveRanges = new List<(DateTime Start, DateTime End)>();
        var leaveRepo = _unitOfWork.Repository<LeaveRequest>();
        if (leaveRepo != null)
        {
            var leaves = await leaveRepo.Query()
                .Include(lr => lr.AdministrativeRequest)
                .Where(lr => lr.AdministrativeRequest != null
                          && lr.AdministrativeRequest.StudentId == studentId
                          && lr.AdministrativeRequest.RequestType == RequestTypes.Leave
                          && lr.AdministrativeRequest.Status == RequestStatus.Approved
                          && !lr.KeepLunchService
                          && (lr.Session == LeaveSession.FullDay || lr.Session == LeaveSession.Morning)
                          && lr.StartDate <= endOfMonth
                          && lr.EndDate >= startOfMonth)
                .Select(lr => new { lr.StartDate, lr.EndDate })
                .ToListAsync(cancellationToken);

            approvedLeaveRanges = leaves.Select(l => (l.StartDate.Date, l.EndDate.Date)).ToList();
        }

        var days = new List<StudentLunchDayDto>();
        for (var cur = startOfMonth; cur <= endOfMonth; cur = cur.AddDays(1))
        {
            serviceDays.TryGetValue(cur, out var sDay);
            registrations.TryGetValue(cur, out var reg);
            bool? isCalSchoolDay = calendarMap.TryGetValue(cur, out var calFlag) ? calFlag : null;

            var resolved = ResolveServiceDay(cur, sDay, isCalSchoolDay);
            var cutoffPassed = IsCutoffPassed(cur, resolved.Cutoff, resolved.IsLocked);
            var hasApprovedLeave = approvedLeaveRanges.Any(r => r.Start <= cur && r.End >= cur);
            dayDishesMap.TryGetValue(cur, out var dayDishes);
            var menusDto = resolved.IsServing ? LunchMenuService.BuildDailyMenusDto(dayDishes) : null;
            var effectiveMealType = reg?.MealType ?? studentProfileDto.DefaultMealType;
            var servedDishes = resolved.IsServing ? SelectDishesForMealType(dayDishes, effectiveMealType) : new List<Dish>();
            var contextualDayNote = contextualStudentNote ?? AllergenCatalog.ExtractExplicitAllergyNote(reg?.Remarks);
            var conflicts = AllergenCatalog.DetectConflicts(studentAllergyTags, trustedAllergyNotes, servedDishes, contextualDayNote);

            days.Add(new StudentLunchDayDto
            {
                Date = cur,
                IsServing = resolved.IsServing,
                CutoffTime = resolved.Cutoff.ToString(@"hh\:mm"),
                IsLocked = resolved.IsLocked,
                IsCutoffPassed = cutoffPassed,
                HasApprovedLeave = hasApprovedLeave,
                ServiceRemarks = resolved.Remarks,
                Menus = menusDto,
                RegistrationId = reg?.Id,
                IsRegistered = reg != null && reg.IsActive,
                MealType = reg?.MealType,
                StudentRemarks = reg?.Remarks,
                AllergyConflicts = conflicts,
                CancellationSource = (reg != null && !reg.IsActive) ? reg.CancellationSource : null,
                CancelledAt = (reg != null && !reg.IsActive) ? UtcDateTimeHelper.Normalize(reg.CancelledAt) : null,
                CancellationReason = (reg != null && !reg.IsActive) ? reg.CancellationReason : null
            });
        }

        var className = student.ClassAssignments
            .Where(ca => ca.IsActive)
            .OrderByDescending(ca => ca.StartDate)
            .ThenByDescending(ca => ca.Id)
            .FirstOrDefault()?.Class?.ClassName ?? "Chưa phân lớp";

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
            MealProfile = studentProfileDto,
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

        var isStaffOrAdmin = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.ServiceStaff);
        var profileMap = await GetStudentMealProfileMapAsync(new[] { request.StudentId }, cancellationToken);
        profileMap.TryGetValue(request.StudentId, out var mealProfile);

        // Phụ huynh chỉ được đăng ký bữa ăn theo chế độ nhà trường phân bổ (mặc định StandardLunch); không được tự chọn loại suất riêng từng bữa
        var effectiveMealType = isStaffOrAdmin
            ? request.MealType
            : (mealProfile?.DefaultMealType ?? MealType.StandardLunch);

        // D-B: Chế độ ăn đặc biệt bắt buộc có mô tả dị ứng / kiêng khem cụ thể (fallback từ StudentMealProfile).
        // Suất thường không tự chép AllergyNotes vào Remarks để tách bạch hồ sơ dị ứng phụ huynh với ghi chú chế biến bếp từng ngày.
        var trimmedRemarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        if (effectiveMealType == MealType.SpecialDiet && trimmedRemarks == null && mealProfile != null)
        {
            if (!string.IsNullOrWhiteSpace(mealProfile.AllergyNotes))
            {
                trimmedRemarks = mealProfile.AllergyNotes.Trim();
            }
            else
            {
                var tagLabels = AllergenCatalog.ParseTags(mealProfile.AllergenTags)
                    .Select(AllergenCatalog.GetLabel)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();
                if (tagLabels.Count > 0)
                {
                    trimmedRemarks = $"Dị ứng: {string.Join(", ", tagLabels)}";
                }
            }
        }

        if (effectiveMealType == MealType.SpecialDiet && string.IsNullOrWhiteSpace(trimmedRemarks))
        {
            return ApiResponse<BatchLunchRegistrationResultDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Khi chọn chế độ ăn đặc biệt, vui lòng nhập mô tả dị ứng / kiêng khem cụ thể để nhà bếp chuẩn bị." });
        }

        // D-C: Chặn ghi chú vượt độ dài cột DB (250 ký tự)
        if (trimmedRemarks != null && trimmedRemarks.Length > MaxRemarksLength)
        {
            return ApiResponse<BatchLunchRegistrationResultDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Ghi chú suất ăn không được vượt quá {MaxRemarksLength} ký tự." });
        }

        var student = await _unitOfWork.Repository<Student>()
            .Query()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);

        if (student == null)
        {
            return ApiResponse<BatchLunchRegistrationResultDto>.Fail(SystemMessages.MSG11);
        }

        if (!isStaffOrAdmin && userId > 0)
        {
            var isLinked = await IsParentLinkedToStudentAsync(userId, request.StudentId, cancellationToken);
            if (!isLinked)
            {
                return ApiResponse<BatchLunchRegistrationResultDto>.Fail(SystemMessages.MSG10);
            }
        }

        var targetDates = request.Dates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();

        // D-A: Không cho phép đăng ký lại suất ăn trên ngày đang có đơn xin nghỉ đã được phê duyệt (buổi Sáng / Cả ngày, không giữ suất ăn)
        var leaveRepo = _unitOfWork.Repository<LeaveRequest>();
        if (leaveRepo != null && targetDates.Count > 0)
        {
            var minTarget = targetDates.First();
            var maxTarget = targetDates.Last();
            var approvedLeaves = await leaveRepo.Query()
                .Include(lr => lr.AdministrativeRequest)
                .Where(lr => lr.AdministrativeRequest != null
                          && lr.AdministrativeRequest.StudentId == request.StudentId
                          && lr.AdministrativeRequest.RequestType == RequestTypes.Leave
                          && lr.AdministrativeRequest.Status == RequestStatus.Approved
                          && !lr.KeepLunchService
                          && (lr.Session == LeaveSession.FullDay || lr.Session == LeaveSession.Morning)
                          && lr.StartDate <= maxTarget
                          && lr.EndDate >= minTarget)
                .ToListAsync(cancellationToken);

            if (approvedLeaves.Count > 0)
            {
                var conflictDates = targetDates
                    .Where(d => approvedLeaves.Any(lr => lr.StartDate.Date <= d && lr.EndDate.Date >= d))
                    .ToList();
                if (conflictDates.Count > 0)
                {
                    var conflictStr = string.Join(", ", conflictDates.Select(d => d.ToString("dd/MM/yyyy")));
                    return ApiResponse<BatchLunchRegistrationResultDto>.Fail(
                        SystemMessages.MSG09,
                        new List<string> { $"Học sinh đã có đơn xin nghỉ được phê duyệt cho ngày {conflictStr}. Vui lòng thu hồi đơn xin nghỉ trước khi đăng ký lại suất ăn." });
                }
            }
        }

        var serviceDays = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .Where(d => targetDates.Contains(d.Date))
            .ToDictionaryAsync(d => d.Date.Date, cancellationToken);
        var calendarMap = targetDates.Count > 0
            ? await GetCalendarDayMapAsync(targetDates.First(), targetDates.Last(), cancellationToken)
            : new Dictionary<DateTime, bool>();

        var calendarSchoolDays = targetDates.Count > 0
            ? await _calendarService.GetSchoolDaysInRangeAsync(targetDates.First(), targetDates.Last(), cancellationToken)
            : new HashSet<DateTime>();

        // Kiểm tra ngày phục vụ (MSG28) và hạn đăng ký (MSG27)
        foreach (var d in targetDates)
        {
            serviceDays.TryGetValue(d, out var sDay);
            bool? isCalSchoolDay = calendarMap.TryGetValue(d, out var calFlag) ? calFlag : null;
            var resolved = ResolveServiceDay(d, sDay, isCalSchoolDay);

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
                existing.IsWaitlisted = false;
                existing.MealType = effectiveMealType;
                existing.Remarks = trimmedRemarks;
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
                    MealType = effectiveMealType,
                    IsActive = true,
                    IsWaitlisted = false,
                    Remarks = trimmedRemarks,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<LunchRegistration>().AddAsync(newReg, cancellationToken);
                registeredDates.Add(d);
            }
        }

        // Gửi thông báo xác nhận (UC-29)
        if (userId > 0 && registeredDates.Count > 0)
        {
            var dateListStr = string.Join(", ", registeredDates.Take(5).Select(d => d.ToString("dd/MM/yyyy"))) +
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

        var trimmedCancelReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (trimmedCancelReason != null && trimmedCancelReason.Length > MaxCancelReasonLength)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Lý do hủy suất ăn không được vượt quá {MaxCancelReasonLength} ký tự." });
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

        var resolved = ResolveServiceDay(targetDate, serviceDay, await GetCalendarSchoolDayFlagAsync(targetDate, cancellationToken));

        // Kiểm tra hạn hủy (UC-30): nếu quá hạn chốt hoặc đã khóa ngày -> thông báo MSG27 và giữ nguyên đăng ký hiện tại
        if (IsCutoffPassed(targetDate, resolved.Cutoff, resolved.IsLocked))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG27);
        }

        reg.IsActive = false;
        reg.IsWaitlisted = false;
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
        var activeRegs = (await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Where(r => r.Date == targetDate && r.IsActive && !r.IsWaitlisted)
            .ToListAsync(cancellationToken))
            .GroupBy(r => r.StudentId)
            .Select(g => g.OrderByDescending(x => x.Id).First())
            .ToList();

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

        var resolved = ResolveServiceDay(targetDate, serviceDay, await GetCalendarSchoolDayFlagAsync(targetDate, cancellationToken));
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

    public async Task<ApiResponse<bool>> RestoreMealForStudentAsync(
        int studentId,
        DateTime mealDate,
        string reason,
        int userId = 0,
        IReadOnlyList<UserRoles>? roles = null,
        CancellationToken cancellationToken = default)
    {
        var targetDate = mealDate.Date;
        var serviceDayRepo = _unitOfWork.Repository<LunchServiceDay>();
        var serviceDay = serviceDayRepo != null
            ? await serviceDayRepo.Query().FirstOrDefaultAsync(d => d.Date == targetDate, cancellationToken)
            : null;
        var resolved = ResolveServiceDay(targetDate, serviceDay, await GetCalendarSchoolDayFlagAsync(targetDate, cancellationToken));
        var afterCutoff = IsCutoffPassed(targetDate, resolved.Cutoff, resolved.IsLocked);

        // Phương án A (SSAWP-135): Sau giờ chốt / đã khóa ngày, chặn Teacher/OfficeStaff; chỉ ServiceStaff/Admin được khôi phục ngoại lệ kèm lý do + AuditLog + thông báo bếp
        if (afterCutoff)
        {
            var isKitchenOrAdmin = roles != null && (roles.Contains(UserRoles.ServiceStaff) || roles.Contains(UserRoles.Admin));
            if (!isKitchenOrAdmin)
            {
                return ApiResponse<bool>.Fail(
                    SystemMessages.MSG27,
                    new List<string> { $"Đã qua giờ chốt ({resolved.Cutoff:hh\\:mm}) hoặc ngày đã khóa. Chỉ Nhân viên bán trú / Quản trị viên mới được phép khôi phục ngoại lệ." });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return ApiResponse<bool>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Bắt buộc phải nhập lý do khi khôi phục suất ăn sau giờ chốt." });
            }
        }

        var reg = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Date == targetDate, cancellationToken);

        if (reg != null && !reg.IsActive)
        {
            var cleanReason = reason?.Trim() ?? string.Empty;
            reg.IsActive = true;
            reg.IsWaitlisted = false;
            reg.CancellationSource = null;
            reg.CancelledAt = null;
            reg.CancelledByUserId = null;
            reg.CancellationReason = null;
            var restoreTag = $"[Khôi phục suất ăn: {cleanReason} - {_clock.Now:HH:mm dd/MM/yyyy}]";
            var existingRemarks = reg.Remarks?.Trim();
            reg.Remarks = !string.IsNullOrWhiteSpace(existingRemarks) && !existingRemarks.StartsWith("[Khôi phục", StringComparison.OrdinalIgnoreCase)
                ? $"{existingRemarks} | {restoreTag}"
                : restoreTag;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditService.LogActionAsync(
                userId > 0 ? userId : null,
                AuditActions.LunchMealRestore,
                nameof(LunchRegistration),
                reg.Id.ToString(),
                new { IsActive = false },
                new { IsActive = true, StudentId = studentId, Date = targetDate.ToString("yyyy-MM-dd"), AfterCutoff = afterCutoff, Reason = cleanReason },
                null,
                cancellationToken);

            if (afterCutoff)
            {
                var roleRepo = _unitOfWork.Repository<Role>();
                var userRoleRepo = _unitOfWork.Repository<UserRole>();
                var notifRepo = _unitOfWork.Repository<Notification>();
                if (roleRepo != null && userRoleRepo != null && notifRepo != null)
                {
                    var kitchenRole = await roleRepo.Query().FirstOrDefaultAsync(r => r.Name == "ServiceStaff", cancellationToken);
                    if (kitchenRole != null)
                    {
                        var kitchenUserIds = await userRoleRepo.Query()
                            .Where(ur => ur.RoleId == kitchenRole.Id)
                            .Select(ur => ur.UserId)
                            .Distinct()
                            .ToListAsync(cancellationToken);

                        foreach (var kUid in kitchenUserIds)
                        {
                            await notifRepo.AddAsync(new Notification
                            {
                                UserId = kUid,
                                Title = "Cảnh báo: Khôi phục suất ăn sau giờ chốt",
                                Content = $"Suất ăn ngày {targetDate:dd/MM/yyyy} của học sinh {reg.Student?.FullName ?? $"#{studentId}"} đã được khôi phục ngoại lệ sau giờ chốt ({resolved.Cutoff:hh\\:mm}). Lý do: {cleanReason}",
                                LinkUrl = "/service/headcount?tab=logs",
                                CreatedAt = DateTime.UtcNow,
                                IsRead = false
                            }, cancellationToken);
                        }
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Ok(true, "Đã khôi phục suất ăn");
        }

        return ApiResponse<bool>.Ok(false, "Học sinh chưa có bản ghi đăng ký bán trú bị hủy cho ngày này");
    }

    private static ClassAssignment? GetPrimaryClassAssignment(LunchRegistration reg, HashSet<int>? scopedClassIds = null)
    {
        var query = reg.Student?.ClassAssignments.Where(ca => ca.IsActive);
        if (query == null) return null;
        if (scopedClassIds != null)
        {
            query = query.Where(ca => scopedClassIds.Contains(ca.ClassId));
        }

        return query
            .OrderByDescending(ca => ca.StartDate)
            .ThenByDescending(ca => ca.Id)
            .FirstOrDefault();
    }

    private async Task<List<Class>> GetActiveScopedClassesAsync(
        HashSet<int>? allowedClassIds,
        int? classId,
        CancellationToken cancellationToken)
    {
        var syRepo = _unitOfWork.Repository<SchoolYear>();
        int? activeSchoolYearId = null;
        if (syRepo != null)
        {
            activeSchoolYearId = await syRepo.Query()
                .Where(sy => sy.IsActive)
                .OrderByDescending(sy => sy.Id)
                .Select(sy => (int?)sy.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var query = _unitOfWork.Repository<Class>()
            .Query()
            .Where(c => c.Status == "Active");

        if (activeSchoolYearId.HasValue)
        {
            query = query.Where(c => c.SchoolYearId == activeSchoolYearId.Value || c.SchoolYearId == 0);
        }
        if (allowedClassIds != null)
        {
            query = query.Where(c => allowedClassIds.Contains(c.Id));
        }
        if (classId.HasValue)
        {
            query = query.Where(c => c.Id == classId.Value);
        }

        return await query.OrderBy(c => c.Grade).ThenBy(c => c.ClassName).ToListAsync(cancellationToken);
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

        var resolved = ResolveServiceDay(targetDate, serviceDay, await GetCalendarSchoolDayFlagAsync(targetDate, cancellationToken));

        // Xác định phạm vi quyền (UC-33: Nhân viên bán trú vận hành; BGH xem toàn trường; Giáo viên xem lớp mình phụ trách)
        if (roles != null &&
            !roles.Contains(UserRoles.ServiceStaff) &&
            !roles.Contains(UserRoles.SchoolBoard) &&
            !roles.Contains(UserRoles.Teacher))
        {
            return ApiResponse<DailyMealBoardDto>.Fail("Bạn không có quyền xem bảng suất ăn bán trú.");
        }

        bool canManage = roles == null || roles.Contains(UserRoles.ServiceStaff);
        bool isTeacherOnly = roles != null &&
                             roles.Contains(UserRoles.Teacher) &&
                             !canManage &&
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

        // 2. Lấy toàn bộ đăng ký ăn bán trú của ngày (chỉ đọc, gom duy nhất 1 bản ghi / học sinh - SSAWP-130/131/132)
        var rawRegistrations = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                    .ThenInclude(ca => ca.Class)
            .Where(r => r.Date == targetDate)
            .ToListAsync(cancellationToken);

        var registrations = rawRegistrations
            .GroupBy(r => r.StudentId)
            .Select(g => g.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Id).First())
            .ToList();

        // 3. Phân bổ theo lớp thuộc năm học đang hoạt động trong phạm vi quyền
        var allClasses = await GetActiveScopedClassesAsync(allowedClassIds, classId, cancellationToken);
        var scopedClassIds = allClasses.Select(c => c.Id).ToHashSet();

        // Lọc registrations theo phạm vi lớp được xem (nếu toàn trường không lọc classId và không phải Teacher-only thì giữ cả HS chưa phân lớp để khớp tổng)
        var isFullSchoolView = allowedClassIds == null && !classId.HasValue;
        var scopedRegistrations = registrations
            .Where(r =>
            {
                var primaryCa = GetPrimaryClassAssignment(r);
                if (primaryCa != null) return scopedClassIds.Contains(primaryCa.ClassId);
                return isFullSchoolView;
            })
            .ToList();

        var totalInitial = scopedRegistrations.Count(r => !r.IsWaitlisted);
        var totalCancelled = scopedRegistrations.Count(r => !r.IsActive && !r.IsWaitlisted);
        var totalActive = scopedRegistrations.Count(r => r.IsActive && !r.IsWaitlisted);

        var classBreakdowns = new List<ClassMealSummaryDto>();
        foreach (var cls in allClasses)
        {
            var classRegs = scopedRegistrations.Where(r => GetPrimaryClassAssignment(r)?.ClassId == cls.Id).ToList();

            classBreakdowns.Add(new ClassMealSummaryDto
            {
                ClassId = cls.Id,
                ClassName = cls.ClassName,
                Grade = cls.Grade,
                InitialRegistered = classRegs.Count(r => !r.IsWaitlisted),
                CancelledCount = classRegs.Count(r => !r.IsActive && !r.IsWaitlisted),
                ActualCount = classRegs.Count(r => r.IsActive && !r.IsWaitlisted),
                StandardCount = classRegs.Count(r => r.IsActive && !r.IsWaitlisted && r.MealType == MealType.StandardLunch),
                VegetarianCount = classRegs.Count(r => r.IsActive && !r.IsWaitlisted && r.MealType == MealType.VegetarianLunch),
                SpecialDietCount = classRegs.Count(r => r.IsActive && !r.IsWaitlisted && r.MealType == MealType.SpecialDiet)
            });
        }

        // BD-01: Nếu xem toàn trường và có học sinh chưa phân lớp đăng ký suất ăn, hiển thị thêm nhóm "Chưa phân lớp" để tổng cột khớp 100% KPI
        if (isFullSchoolView)
        {
            var unassignedRegs = scopedRegistrations
                .Where(r => GetPrimaryClassAssignment(r) == null)
                .ToList();
            if (unassignedRegs.Count > 0)
            {
                classBreakdowns.Add(new ClassMealSummaryDto
                {
                    ClassId = 0,
                    ClassName = "Chưa phân lớp",
                    Grade = 0,
                    InitialRegistered = unassignedRegs.Count(r => !r.IsWaitlisted),
                    CancelledCount = unassignedRegs.Count(r => !r.IsActive && !r.IsWaitlisted),
                    ActualCount = unassignedRegs.Count(r => r.IsActive && !r.IsWaitlisted),
                    StandardCount = unassignedRegs.Count(r => r.IsActive && !r.IsWaitlisted && r.MealType == MealType.StandardLunch),
                    VegetarianCount = unassignedRegs.Count(r => r.IsActive && !r.IsWaitlisted && r.MealType == MealType.VegetarianLunch),
                    SpecialDietCount = unassignedRegs.Count(r => r.IsActive && !r.IsWaitlisted && r.MealType == MealType.SpecialDiet)
                });
            }
        }

        // 4. Danh sách học sinh tối thiểu phục vụ vận hành (UC-33) + Thực đơn thật & Cảnh báo dị ứng tự động
        var dayDishesMap = await GetServiceDayDishesMapAsync(targetDate, targetDate, cancellationToken);
        dayDishesMap.TryGetValue(targetDate, out var todayDishes);
        var todayMenusDto = resolved.IsServing ? LunchMenuService.BuildDailyMenusDto(todayDishes) : null;
        var profileMap = await GetStudentMealProfileMapAsync(scopedRegistrations.Select(r => r.StudentId), cancellationToken);

        var studentMeals = scopedRegistrations
            .Select(r =>
            {
                var activeCa = GetPrimaryClassAssignment(r);
                profileMap.TryGetValue(r.StudentId, out var mealProfile);
                var studentTags = mealProfile != null ? AllergenCatalog.ParseTags(mealProfile.AllergenTags) : new List<string>();
                var trustedAllergyNotes = string.IsNullOrWhiteSpace(mealProfile?.AllergyNotes) ? null : mealProfile.AllergyNotes.Trim();
                var contextualNotes = AllergenCatalog.ExtractExplicitAllergyNote(r.Student?.SpecialNeedsNotes)
                    ?? AllergenCatalog.ExtractExplicitAllergyNote(r.Remarks);

                var servedDishes = resolved.IsServing ? SelectDishesForMealType(todayDishes, r.MealType) : new List<Dish>();
                var conflicts = r.IsActive
                    ? AllergenCatalog.DetectConflicts(studentTags, trustedAllergyNotes, servedDishes, contextualNotes)
                    : new List<string>();

                var allergyWarning = AllergenCatalog.FormatAllergySummary(studentTags, trustedAllergyNotes, contextualNotes);

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
                    StudentAllergenTags = studentTags,
                    StudentAllergyNotes = trustedAllergyNotes,
                    AllergyWarning = allergyWarning,
                    AllergyConflicts = conflicts,
                    CancellationSource = r.CancellationSource,
                    CancelledAt = UtcDateTimeHelper.Normalize(r.CancelledAt),
                    CancellationReason = r.CancellationReason
                };
            })
            .OrderBy(s => s.ClassId == 0 ? 1 : 0)
            .ThenBy(s => s.Grade)
            .ThenBy(s => s.ClassName)
            .ThenBy(s => s.StudentName)
            .ToList();

        // 5. Danh sách nhật ký biến động suất ăn (Audit Log): hủy bởi PH (UC-30), tự hủy do đơn nghỉ (WFL 1.2.3), hoặc khôi phục
        var cutoffAt = targetDate.Add(resolved.Cutoff);
        var logs = new List<MealAuditLogDto>();
        foreach (var reg in scopedRegistrations.Where(r => (!r.IsActive && !r.IsWaitlisted) || (r.Remarks != null && r.Remarks.Contains("Khôi phục"))))
        {
            var studentClass = GetPrimaryClassAssignment(reg)?.Class?.ClassName ?? "Chưa phân lớp";
            var isCancelled = !reg.IsActive;
            var changeType = isCancelled
                ? (reg.CancellationSource == LunchCancellationSource.Parent || (reg.CancellationReason != null && reg.CancellationReason.Contains("Phụ huynh"))
                    ? "ParentCancelled"
                    : "AutoCancelled")
                : "Restored";
            var changedAt = isCancelled && reg.CancelledAt.HasValue ? reg.CancelledAt.Value : reg.CreatedAt;
            var changedAtUtc = UtcDateTimeHelper.Normalize(changedAt);

            logs.Add(new MealAuditLogDto
            {
                StudentId = reg.StudentId,
                StudentCode = reg.Student?.StudentCode ?? string.Empty,
                StudentName = reg.Student?.FullName ?? string.Empty,
                ClassName = studentClass,
                ChangeType = changeType,
                Reason = (isCancelled ? reg.CancellationReason : null) ?? reg.Remarks ?? string.Empty,
                Timestamp = changedAtUtc,
                IsBeforeCutoff = _clock.ToSchoolTime(changedAtUtc) < cutoffAt
            });
        }

        // 6. Kiểm tra cảnh báo bất thường: Có đơn hủy suất nhưng điểm danh thực tế lại là "Có mặt"
        var attendanceToday = await _unitOfWork.Repository<AttendanceRecord>()
            .Query()
            .Where(a => a.Date == targetDate)
            .ToListAsync(cancellationToken);

        var totalAnomalies = scopedRegistrations.Count(r =>
            !r.IsActive && !r.IsWaitlisted &&
            attendanceToday.Any(a => a.StudentId == r.StudentId && a.Status == AttendanceStatus.Present));

        // 7. Tra cứu người chốt sổ gần nhất từ AuditLog (nếu ngày đang khóa)
        string? lockedByName = null;
        if (resolved.IsLocked && serviceDay != null)
        {
            var auditRepo = _unitOfWork.Repository<AuditLog>();
            if (auditRepo != null)
            {
                var lastLock = await auditRepo.Query()
                    .Include(a => a.User)
                    .Where(a => a.Action == AuditActions.LunchServiceDayLock
                        && a.EntityName == nameof(LunchServiceDay)
                        && a.EntityId == serviceDay.Id.ToString())
                    .OrderByDescending(a => a.Timestamp)
                    .FirstOrDefaultAsync(cancellationToken);
                lockedByName = lastLock?.User?.FullName ?? lastLock?.User?.Username;
            }
        }

        var board = new DailyMealBoardDto
        {
            Date = targetDate,
            IsServing = resolved.IsServing,
            IsLocked = resolved.IsLocked,
            LockedAt = resolved.LockedAt,
            LockedByName = lockedByName,
            CutoffTime = resolved.Cutoff.ToString(@"hh\:mm"),
            ServiceRemarks = resolved.Remarks,
            Menus = todayMenusDto,
            CanManage = canManage,
            TotalInitialRegistered = totalInitial,
            TotalAutoCancelled = totalCancelled,
            TotalActiveToCook = totalActive,
            TotalAnomalies = totalAnomalies,
            ClassBreakdowns = classBreakdowns
                .OrderBy(c => c.ClassId == 0 ? 1 : 0)
                .ThenBy(c => c.Grade)
                .ThenBy(c => c.ClassName)
                .ToList(),
            MealChangeLogs = logs.OrderByDescending(l => l.Timestamp).ToList(),
            StudentMeals = studentMeals
        };

        return ApiResponse<DailyMealBoardDto>.Ok(board);
    }

    public async Task<ApiResponse<bool>> LockDailyMealAsync(LockMealDayRequestDto request, int userId = 0, CancellationToken cancellationToken = default)
    {
        var targetDate = request.Date.Date;
        if (targetDate > _clock.Today)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Chỉ được chốt/khóa suất ăn cho ngày hôm nay hoặc ngày đã qua." });
        }

        var trimmedLockNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (trimmedLockNotes != null && trimmedLockNotes.Length > MaxRemarksLength)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Ghi chú chốt suất không được vượt quá {MaxRemarksLength} ký tự." });
        }

        var serviceDay = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .FirstOrDefaultAsync(d => d.Date == targetDate, cancellationToken);

        if (serviceDay != null && serviceDay.IsLocked)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG09,
                new List<string> { "Ngày đã chọn đã được chốt sổ bán trú trước đó." });
        }

        var resolvedDay = ResolveServiceDay(targetDate, serviceDay, await GetCalendarSchoolDayFlagAsync(targetDate, cancellationToken));
        var cutoff = resolvedDay.Cutoff;

        if (serviceDay == null)
        {
            serviceDay = new LunchServiceDay
            {
                Date = targetDate,
                IsServing = resolvedDay.IsServing,
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

        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            AuditActions.LunchServiceDayLock,
            nameof(LunchServiceDay),
            serviceDay.Id.ToString(),
            new { IsLocked = false },
            new { IsLocked = true, Date = targetDate.ToString("yyyy-MM-dd"), Notes = request.Notes?.Trim() },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, $"Đã chốt danh sách suất ăn gửi nhà bếp thành công (mốc {cutoff:hh\\:mm})");
    }

    public async Task<ApiResponse<bool>> UnlockDailyMealAsync(int userId, UnlockMealDayRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Bắt buộc phải nhập lý do mở khóa ngày bán trú." });
        }

        var targetDate = request.Date.Date;
        var serviceDay = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .FirstOrDefaultAsync(d => d.Date == targetDate, cancellationToken);

        if (serviceDay == null || !serviceDay.IsLocked)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG09,
                new List<string> { "Ngày đã chọn hiện không ở trạng thái bị khóa." });
        }

        var oldLockedAt = serviceDay.LockedAt;
        serviceDay.IsLocked = false;
        serviceDay.LockedAt = null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            AuditActions.LunchServiceDayUnlock,
            nameof(LunchServiceDay),
            serviceDay.Id.ToString(),
            new { IsLocked = true, LockedAt = oldLockedAt },
            new { IsLocked = false, Date = targetDate.ToString("yyyy-MM-dd"), Reason = request.Reason.Trim() },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, $"Đã mở khóa ngày bán trú {targetDate:dd/MM/yyyy} thành công.");
    }

    public async Task<ApiResponse<MealHeadcountDto>> GetMealHeadcountAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var serviceDays = await _unitOfWork.Repository<LunchServiceDay>()
            .FindAsync(d => d.Date == targetDate, cancellationToken);
        var day = serviceDays.FirstOrDefault();

        var resolved = ResolveServiceDay(targetDate, day, await GetCalendarSchoolDayFlagAsync(targetDate, cancellationToken));
        bool isServiceDay = resolved.IsServing;
        var finalizeTime = resolved.Cutoff;
        bool isFinalized = IsCutoffPassed(targetDate, finalizeTime, resolved.IsLocked);

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
        var isStaffOrAdmin = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.ServiceStaff);
        if (!isStaffOrAdmin)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG10,
                new List<string> { "Phụ huynh không có quyền tự thay đổi loại suất ăn từng bữa; vui lòng khai báo dị ứng để Tổ Bán trú bố trí khẩu phần phù hợp." });
        }

        if (request.StudentId <= 0)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01);
        }

        // D-C: Ghi chú không vượt quá 250 ký tự
        var trimmedRemarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        if (trimmedRemarks != null && trimmedRemarks.Length > MaxRemarksLength)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Ghi chú suất ăn không được vượt quá {MaxRemarksLength} ký tự." });
        }

        var targetDate = request.Date.Date;

        // D-B: Nếu phân bổ Chế độ đặc biệt, chỉ bắt buộc nhập ghi chú chế biến bếp khi:
        // (1) ngày đó chưa cấu hình thực đơn Đặc biệt riêng, HOẶC (2) thực đơn Đặc biệt vẫn còn món trùng dị ứng của học sinh.
        if (request.MealType == MealType.SpecialDiet && string.IsNullOrWhiteSpace(trimmedRemarks))
        {
            var dayDishesMap = await GetServiceDayDishesMapAsync(targetDate, targetDate, cancellationToken);
            dayDishesMap.TryGetValue(targetDate, out var todayDishes);
            var specialMenuDishes = todayDishes?
                .Where(d => d.MealType == MealType.SpecialDiet && d.Dish != null)
                .Select(d => d.Dish)
                .ToList() ?? new List<Dish>();

            if (specialMenuDishes.Count == 0)
            {
                return ApiResponse<bool>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Khi chọn chế độ ăn đặc biệt mà ngày này chưa cấu hình thực đơn Đặc biệt riêng, vui lòng nhập mô tả món / kiêng khem để nhà bếp chuẩn bị." });
            }

            var profileMap = await GetStudentMealProfileMapAsync(new[] { request.StudentId }, cancellationToken);
            profileMap.TryGetValue(request.StudentId, out var mealProfile);
            var studentTags = mealProfile != null ? AllergenCatalog.ParseTags(mealProfile.AllergenTags) : new List<string>();
            var remainingConflicts = AllergenCatalog.DetectConflicts(studentTags, mealProfile?.AllergyNotes, specialMenuDishes);
            if (remainingConflicts.Count > 0)
            {
                return ApiResponse<bool>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { $"Thực đơn Đặc biệt ngày này vẫn còn món trùng dị ứng ({string.Join("; ", remainingConflicts)}). Vui lòng nhập ghi chú đổi/bỏ món cho nhà bếp." });
            }
        }

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

        var resolved = ResolveServiceDay(targetDate, serviceDay, await GetCalendarSchoolDayFlagAsync(targetDate, cancellationToken));
        if (resolved.IsLocked)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG27,
                new List<string> { "Ngày bán trú đã khóa sổ nấu, vui lòng mở khóa trước khi điều chỉnh chế độ khẩu phần." });
        }

        var oldVal = new { reg.MealType, reg.Remarks };
        reg.MealType = request.MealType;
        reg.Remarks = request.Remarks?.Trim();

        await _auditService.LogActionAsync(
            userId,
            AuditActions.LunchRegistrationUpdateMealType,
            nameof(LunchRegistration),
            reg.Id.ToString(),
            oldVal,
            new { reg.MealType, reg.Remarks },
            null, // TODO: pass the client IP address; it is not recorded yet.
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

        if (roles != null &&
            !roles.Contains(UserRoles.ServiceStaff) &&
            !roles.Contains(UserRoles.SchoolBoard) &&
            !roles.Contains(UserRoles.Teacher))
        {
            return ApiResponse<LunchPeriodReportDto>.Fail("Bạn không có quyền xem báo cáo thống kê suất ăn bán trú.");
        }

        // ponytail: nếu là Giáo viên (Teacher), chỉ cho phép xem lớp mình chủ nhiệm
        bool isTeacherOnly = roles != null &&
                             roles.Contains(UserRoles.Teacher) &&
                             !roles.Contains(UserRoles.ServiceStaff) &&
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

        var serviceDays = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .Where(d => d.Date >= start && d.Date <= end)
            .ToDictionaryAsync(d => d.Date.Date, cancellationToken);
        var calendarMap = await GetCalendarDayMapAsync(start, end, cancellationToken);

        int servingCount = 0;
        for (var cur = start; cur <= end; cur = cur.AddDays(1))
        {
            serviceDays.TryGetValue(cur, out var sDay);
            bool? isCalSchoolDay = calendarMap.TryGetValue(cur, out var calFlag) ? calFlag : null;
            if (ResolveServiceDay(cur, sDay, isCalSchoolDay).IsServing) servingCount++;
        }

        var classes = await GetActiveScopedClassesAsync(allowedClassIds, classId, cancellationToken);
        var classIds = classes.Select(c => c.Id).ToHashSet();

        var rawRegs = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
            .Where(r => r.Date >= start && r.Date <= end && !r.IsWaitlisted)
            .ToListAsync(cancellationToken);

        var dedupedRegs = rawRegs
            .GroupBy(r => new { r.StudentId, Date = r.Date.Date })
            .Select(g => g.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Id).First())
            .ToList();

        var isFullSchoolView = allowedClassIds == null && !classId.HasValue;
        var regWithPrimaryClass = dedupedRegs
            .Select(r => new { Reg = r, PrimaryCa = GetPrimaryClassAssignment(r) })
            .Where(x => x.PrimaryCa != null ? classIds.Contains(x.PrimaryCa.ClassId) : isFullSchoolView)
            .Select(x => new { x.Reg, PrimaryClassId = x.PrimaryCa?.ClassId })
            .ToList();

        var scopedRegs = regWithPrimaryClass.Select(x => x.Reg).ToList();

        var summaries = classes.Select(cls =>
        {
            var cRegs = regWithPrimaryClass
                .Where(x => x.PrimaryClassId == cls.Id)
                .Select(x => x.Reg)
                .ToList();
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

        // BD-01: Bổ sung dòng "Chưa phân lớp" khi xem báo cáo toàn trường để đồng bộ tổng với bảng ngày
        if (isFullSchoolView)
        {
            var unassignedRegs = regWithPrimaryClass
                .Where(x => !x.PrimaryClassId.HasValue)
                .Select(x => x.Reg)
                .ToList();
            if (unassignedRegs.Count > 0)
            {
                summaries.Add(new ClassMealSummaryDto
                {
                    ClassId = 0,
                    ClassName = "Chưa phân lớp",
                    Grade = 0,
                    InitialRegistered = unassignedRegs.Count,
                    CancelledCount = unassignedRegs.Count(r => !r.IsActive),
                    ActualCount = unassignedRegs.Count(r => r.IsActive),
                    StandardCount = unassignedRegs.Count(r => r.IsActive && r.MealType == MealType.StandardLunch),
                    VegetarianCount = unassignedRegs.Count(r => r.IsActive && r.MealType == MealType.VegetarianLunch),
                    SpecialDietCount = unassignedRegs.Count(r => r.IsActive && r.MealType == MealType.SpecialDiet)
                });
            }
        }

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
}
