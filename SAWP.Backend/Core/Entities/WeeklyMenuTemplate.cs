using Core.Enums;

namespace Core.Entities;

public class WeeklyMenuTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedByUserId { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<WeeklyMenuTemplateItem> Items { get; set; } = new List<WeeklyMenuTemplateItem>();
}

public class WeeklyMenuTemplateItem
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public virtual WeeklyMenuTemplate Template { get; set; } = null!;

    /// <summary>
    /// Thứ trong tuần: 1 = Thứ 2 ... 6 = Thứ 7 (học bù).
    /// </summary>
    public int DayOfWeek { get; set; }

    public MealType MealType { get; set; } = MealType.StandardLunch;
    public int DishId { get; set; }
    public virtual Dish Dish { get; set; } = null!;
    public int SortOrder { get; set; }
}
