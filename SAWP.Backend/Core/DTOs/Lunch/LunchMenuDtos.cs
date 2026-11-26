using Core.Enums;

namespace Core.DTOs;

public class DishDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DishCategory Category { get; set; }
    public List<MealType> SuitableMealTypes { get; set; } = new();
    public List<string> AllergenTags { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DishSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DishCategory Category { get; set; }
    public List<string> AllergenTags { get; set; } = new();
}

public class UpsertDishRequestDto
{
    public string Name { get; set; } = string.Empty;
    public DishCategory Category { get; set; } = DishCategory.MainDish;
    public List<MealType> SuitableMealTypes { get; set; } = new() { MealType.StandardLunch };
    public List<string> AllergenTags { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class DailyMenusByMealTypeDto
{
    public List<DishSummaryDto> Standard { get; set; } = new();
    public List<DishSummaryDto> Vegetarian { get; set; } = new();
    public List<DishSummaryDto> Special { get; set; } = new();
}

public class WeeklyMenuTemplateItemDto
{
    public int DayOfWeek { get; set; }
    public MealType MealType { get; set; }
    public int DishId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public DishCategory Category { get; set; }
    public List<string> AllergenTags { get; set; } = new();
    public int SortOrder { get; set; }
}

public class WeeklyMenuTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<WeeklyMenuTemplateItemDto> Items { get; set; } = new();
}

public class WeeklyMenuTemplateItemInputDto
{
    public int DayOfWeek { get; set; }
    public MealType MealType { get; set; }
    public int DishId { get; set; }
    public int SortOrder { get; set; }
}

public class UpsertWeeklyMenuTemplateRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<WeeklyMenuTemplateItemInputDto> Items { get; set; } = new();
}

public class ApplyMenuTemplateRequestDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? CutoffTime { get; set; }
    public bool OverwriteExistingMenus { get; set; } = true;
}

public class ApplyMenuTemplateResultDto
{
    public List<DateTime> AppliedDates { get; set; } = new();
    public List<DateTime> SkippedLockedDates { get; set; } = new();
    public List<DateTime> SkippedNonSchoolDates { get; set; } = new();
    public List<DateTime> SkippedExistingDates { get; set; } = new();
}

public class StudentMealProfileDto
{
    public int StudentId { get; set; }
    public MealType DefaultMealType { get; set; } = MealType.StandardLunch;
    public List<string> AllergenTags { get; set; } = new();
    public string? AllergyNotes { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpsertStudentMealProfileRequestDto
{
    public MealType DefaultMealType { get; set; } = MealType.StandardLunch;
    public List<string> AllergenTags { get; set; } = new();
    public string? AllergyNotes { get; set; }
}
