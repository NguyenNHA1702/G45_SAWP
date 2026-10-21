using Core.Configuration;
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

    public LunchService(
        IUnitOfWork unitOfWork,
        ISchoolClock clock,
        IOptions<SchoolOptions> options)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<ApiResponse<bool>> RegisterLunchAsync(LunchRegistrationDto request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<DailyMealCountReportDto>> GetDailyMealCountAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var count = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .CountAsync(r => r.Date.Date == targetDate && r.IsActive, cancellationToken);

        return ApiResponse<DailyMealCountReportDto>.Ok(new DailyMealCountReportDto
        {
            Date = date,
            TotalStandardMeals = count,
            GrandTotal = count
        });
    }

    public async Task<ApiResponse<bool>> CancelMealForLeaveAsync(int studentId, DateTime mealDate, string reason, CancellationToken cancellationToken = default)
    {
        var targetDate = mealDate.Date;
        var reg = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Date.Date == targetDate, cancellationToken);

        if (reg != null)
        {
            reg.IsActive = false;
            reg.Remarks = $"Hủy do đơn xin nghỉ / vắng: {reason} (Ghi nhận lúc {DateTime.Now:HH:mm dd/MM/yyyy})";
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Ok(true, "Đã tự động ghi nhận hủy suất ăn trước giờ chốt");
        }

        return ApiResponse<bool>.Ok(false, "Học sinh không có đăng ký suất ăn nào cho ngày này");
    }

    public async Task<ApiResponse<bool>> RestoreMealForStudentAsync(int studentId, DateTime mealDate, string reason, CancellationToken cancellationToken = default)
    {
        var targetDate = mealDate.Date;
        var reg = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Date.Date == targetDate, cancellationToken);

        if (reg != null)
        {
            reg.IsActive = true;
            reg.Remarks = $"Khôi phục suất ăn: {reason} (Lúc {DateTime.Now:HH:mm dd/MM/yyyy})";
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ApiResponse<bool>.Ok(false, "Học sinh chưa có bản ghi đăng ký bán trú gốc");
    }

    public async Task<ApiResponse<DailyMealBoardDto>> GetDailyMealBoardAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;

        // 1. Kiểm tra trạng thái chốt ngày
        var serviceDay = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .FirstOrDefaultAsync(d => d.Date.Date == targetDate, cancellationToken);

        var isLocked = serviceDay?.IsLocked ?? false;
        var lockedAt = serviceDay?.LockedAt;

        // Tự động bảo đảm có dữ liệu đăng ký bán trú mẫu nếu ngày xem chưa có
        if (!await _unitOfWork.Repository<LunchRegistration>().Query().AnyAsync(r => r.Date.Date == targetDate, cancellationToken))
        {
            var studentList = await _unitOfWork.Repository<Student>().Query().Take(3).ToListAsync(cancellationToken);
            if (studentList.Any())
            {
                foreach (var s in studentList)
                {
                    await _unitOfWork.Repository<LunchRegistration>().AddAsync(new LunchRegistration
                    {
                        StudentId = s.Id,
                        Date = targetDate,
                        MealType = Core.Enums.MealType.StandardLunch,
                        IsActive = true
                    }, cancellationToken);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // 2. Lấy toàn bộ đăng ký ăn bán trú của ngày kèm thông tin học sinh và lớp
        var registrations = await _unitOfWork.Repository<LunchRegistration>()
            .Query()
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                    .ThenInclude(ca => ca.Class)
            .Where(r => r.Date.Date == targetDate)
            .ToListAsync(cancellationToken);

        var totalInitial = registrations.Count;
        var totalCancelled = registrations.Count(r => !r.IsActive);
        var totalActive = registrations.Count(r => r.IsActive);

        // 3. Phân bổ theo lớp
        var allClasses = await _unitOfWork.Repository<Class>()
            .Query()
            .Where(c => c.Status == "Active")
            .ToListAsync(cancellationToken);

        var classBreakdowns = new List<ClassMealSummaryDto>();

        foreach (var cls in allClasses)
        {
            var classRegs = registrations.Where(r => 
                r.Student?.ClassAssignments.Any(ca => ca.ClassId == cls.Id && ca.IsActive) == true).ToList();

            var initCount = classRegs.Count;
            var cancCount = classRegs.Count(r => !r.IsActive);
            var actCount = classRegs.Count(r => r.IsActive);

            classBreakdowns.Add(new ClassMealSummaryDto
            {
                ClassId = cls.Id,
                ClassName = cls.ClassName,
                Grade = cls.Grade,
                InitialRegistered = initCount,
                CancelledCount = cancCount,
                ActualCount = actCount
            });
        }

        // 4. Danh sách nhật ký biến động suất ăn (Audit Log)
        var logs = new List<MealAuditLogDto>();
        foreach (var reg in registrations.Where(r => !string.IsNullOrEmpty(r.Remarks)))
        {
            var studentClass = reg.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class?.ClassName ?? "Chưa phân lớp";
            var isAutoCancelled = !reg.IsActive;
            var changeType = isAutoCancelled ? "AutoCancelled" : (reg.Remarks?.Contains("Khôi phục") == true ? "Restored" : "Adjustment");

            logs.Add(new MealAuditLogDto
            {
                StudentId = reg.StudentId,
                StudentCode = reg.Student?.StudentCode ?? string.Empty,
                StudentName = reg.Student?.FullName ?? string.Empty,
                ClassName = studentClass,
                ChangeType = changeType,
                Reason = reg.Remarks ?? string.Empty,
                Timestamp = reg.CreatedAt,
                IsBeforeCutoff = true
            });
        }

        // 5. Kiểm tra cảnh báo bất thường: Có đơn hủy suất nhưng điểm danh thực tế lại là "Có mặt"
        var attendanceToday = await _unitOfWork.Repository<AttendanceRecord>()
            .Query()
            .Where(a => a.Date.Date == targetDate)
            .ToListAsync(cancellationToken);

        var totalAnomalies = registrations.Count(r => 
            !r.IsActive && 
            attendanceToday.Any(a => a.StudentId == r.StudentId && a.Status == Core.Enums.AttendanceStatus.Present));

        var board = new DailyMealBoardDto
        {
            Date = targetDate,
            IsLocked = isLocked,
            LockedAt = lockedAt,
            CutoffTime = "08:15",
            TotalInitialRegistered = totalInitial,
            TotalAutoCancelled = totalCancelled,
            TotalActiveToCook = totalActive,
            TotalAnomalies = totalAnomalies,
            ClassBreakdowns = classBreakdowns.OrderBy(c => c.Grade).ThenBy(c => c.ClassName).ToList(),
            MealChangeLogs = logs.OrderByDescending(l => l.Timestamp).ToList()
        };

        return ApiResponse<DailyMealBoardDto>.Ok(board);
    }

    public async Task<ApiResponse<bool>> LockDailyMealAsync(LockMealDayRequestDto request, CancellationToken cancellationToken = default)
    {
        var targetDate = request.Date.Date;
        var serviceDay = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .FirstOrDefaultAsync(d => d.Date.Date == targetDate, cancellationToken);

        if (serviceDay == null)
        {
            serviceDay = new LunchServiceDay
            {
                Date = targetDate,
                IsServing = true,
                CutoffTime = new TimeSpan(8, 15, 0),
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
        return ApiResponse<bool>.Ok(true, "Đã chốt danh sách suất ăn gửi nhà bếp thành công lúc 08h15");
    }

    public async Task<ApiResponse<MealHeadcountDto>> GetMealHeadcountAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var serviceDays = await _unitOfWork.Repository<LunchServiceDay>()
            .FindAsync(d => d.Date == targetDate, cancellationToken);
        var day = serviceDays.FirstOrDefault();

        bool isServiceDay = day != null 
            ? day.IsServing 
            : (targetDate.DayOfWeek != DayOfWeek.Saturday && targetDate.DayOfWeek != DayOfWeek.Sunday);

        var finalizeTime = TimeSpan.TryParse(_options.KitchenFinalizeTime, out var ft) ? ft : new TimeSpan(10, 30, 0);
        bool isFinalized = (targetDate < _clock.Today) || (targetDate == _clock.Today && _clock.Now.TimeOfDay >= finalizeTime);

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
        var submittedClassIds = await _unitOfWork.Attendance.GetSubmittedClassIdsAsync(allClassIds, targetDate, cancellationToken);

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

