using Core.Enums;

namespace Core.DTOs;

public class LunchRegistrationDto
{
    public int StudentId { get; set; }
    public DateTime Date { get; set; }
    public MealType MealType { get; set; }
    public bool IsActive { get; set; }
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
    public bool IsFinalized { get; set; }                          // Now >= KitchenFinalizeTime
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
    public string ChangeType { get; set; } = string.Empty; // "AutoCancelled" | "Restored" | "LateAdded"
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsBeforeCutoff { get; set; }
}

public class DailyMealBoardDto
{
    public DateTime Date { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public string CutoffTime { get; set; } = "08:15";
    public int TotalInitialRegistered { get; set; }
    public int TotalAutoCancelled { get; set; }
    public int TotalActiveToCook { get; set; }
    public int TotalAnomalies { get; set; }
    public List<ClassMealSummaryDto> ClassBreakdowns { get; set; } = new();
    public List<MealAuditLogDto> MealChangeLogs { get; set; } = new();
}

public class LockMealDayRequestDto
{
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
}
