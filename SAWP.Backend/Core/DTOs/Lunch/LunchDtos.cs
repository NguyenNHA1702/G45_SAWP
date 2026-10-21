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

