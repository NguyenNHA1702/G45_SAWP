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
