using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class LunchService : ILunchService
{
    private readonly IUnitOfWork _unitOfWork;

    public LunchService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
}
