using Core.Enums;

namespace Core.DTOs;

public class LunchRegistrationDto
{
    public int StudentId { get; set; }
    public DateTime Date { get; set; }
    public MealType MealType { get; set; }
    public bool IsActive { get; set; }
    public string? Remarks { get; set; }
}

public class DailyMealCountReportDto
{
    public DateTime Date { get; set; }
    public int TotalStandardMeals { get; set; }
    public int TotalVegetarianMeals { get; set; }
    public int TotalSpecialDietMeals { get; set; }
    public int GrandTotal { get; set; }
}

public class MealHeadcountDto
{
    public DateTime Date { get; set; }
    public bool IsServiceDay { get; set; }
    public bool IsFinalized { get; set; }                          // Now >= LunchCutoffTime
    public TimeSpan FinalizeTime { get; set; }
    public int TotalMeals { get; set; }
    public int StandardLunch { get; set; }
    public int VegetarianLunch { get; set; }
    public int SpecialDiet { get; set; }
    public int DeductedByAttendance { get; set; }
    public int DeductedByLeave { get; set; }
    public List<ClassHeadcountDto> Classes { get; set; } = new();
}

public class ClassHeadcountDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int Meals { get; set; }
    public int DeductedByAttendance { get; set; }
    public bool AttendanceSubmitted { get; set; }                  // false = số liệu còn tạm tính
}

public class ClassMealSummaryDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public int InitialRegistered { get; set; }
    public int CancelledCount { get; set; }
    public int ActualCount { get; set; }
}

public class MealAuditLogDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty; // "AutoCancelled" | "ParentCancelled" | "Restored" | "LateAdded"
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsBeforeCutoff { get; set; }
}

public class StudentMealOperationItemDto
{
    public int RegistrationId { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public MealType MealType { get; set; }
    public bool IsActive { get; set; }
    public string? Remarks { get; set; }
    public LunchCancellationSource? CancellationSource { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class DailyMealBoardDto
{
    public DateTime Date { get; set; }
    public bool IsServing { get; set; } = true;
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public string CutoffTime { get; set; } = "08:15";
    public string? ServiceRemarks { get; set; }
    public bool CanManage { get; set; }
    public int TotalInitialRegistered { get; set; }
    public int TotalAutoCancelled { get; set; }
    public int TotalActiveToCook { get; set; }
    public int TotalAnomalies { get; set; }
    public List<ClassMealSummaryDto> ClassBreakdowns { get; set; } = new();
    public List<MealAuditLogDto> MealChangeLogs { get; set; } = new();
    public List<StudentMealOperationItemDto> StudentMeals { get; set; } = new();
}

public class LockMealDayRequestDto
{
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
}

// ---- UC-32: Cấu hình ngày phục vụ, hạn đăng ký/hủy và ghi chú/thực đơn ----
public class LunchServiceDayConfigDto
{
    public DateTime Date { get; set; }
    public bool IsServing { get; set; }
    public string CutoffTime { get; set; } = "08:15";
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? Remarks { get; set; }
}

public class UpsertLunchServiceDaysRequestDto
{
    public List<DateTime> Dates { get; set; } = new();
    public bool IsServing { get; set; } = true;
    public string CutoffTime { get; set; } = "08:15";
    public string? Remarks { get; set; }
}

// ---- UC-31: Phụ huynh xem lịch và trạng thái đăng ký bán trú của con ----
public class StudentLunchDayDto
{
    public DateTime Date { get; set; }
    public bool IsServing { get; set; }
    public string CutoffTime { get; set; } = "08:15";
    public bool IsLocked { get; set; }
    public bool IsCutoffPassed { get; set; }
    public string? ServiceRemarks { get; set; }
    public int? RegistrationId { get; set; }
    public bool IsRegistered { get; set; }
    public MealType? MealType { get; set; }
    public string? StudentRemarks { get; set; }
    public LunchCancellationSource? CancellationSource { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class StudentLunchScheduleDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Month { get; set; } = string.Empty; // YYYY-MM
    public int TotalServingDays { get; set; }
    public int TotalRegisteredDays { get; set; }
    public int TotalCancelledDays { get; set; }
    public List<StudentLunchDayDto> Days { get; set; } = new();
}

// ---- UC-29: Phụ huynh đăng ký suất ăn cho 1 hoặc nhiều ngày phục vụ ----
public class BatchLunchRegistrationRequestDto
{
    public int StudentId { get; set; }
    public List<DateTime> Dates { get; set; } = new();
    public MealType MealType { get; set; } = MealType.StandardLunch;
    public string? Remarks { get; set; }
}

public class BatchLunchRegistrationResultDto
{
    public int RegisteredCount { get; set; }
    public List<DateTime> RegisteredDates { get; set; } = new();
    public List<DateTime> SkippedDuplicateDates { get; set; } = new();
}

// ---- UC-30: Phụ huynh hủy đăng ký còn hiệu lực ----
public class CancelLunchRegistrationRequestDto
{
    public int StudentId { get; set; }
    public DateTime Date { get; set; }
    public string? Reason { get; set; }
}
