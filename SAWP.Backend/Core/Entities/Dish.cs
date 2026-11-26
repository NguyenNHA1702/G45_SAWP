using Core.Enums;

namespace Core.Entities;

public class Dish
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DishCategory Category { get; set; } = DishCategory.MainDish;

    /// <summary>
    /// Danh sách chế độ ăn phù hợp (CSV tên enum MealType, vd: "StandardLunch,SpecialDiet").
    /// </summary>
    public string SuitableMealTypes { get; set; } = nameof(MealType.StandardLunch);

    /// <summary>
    /// Nhãn dị ứng chuẩn hóa (CSV mã từ AllergenCatalog, vd: "seafood,egg").
    /// </summary>
    public string? AllergenTags { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<WeeklyMenuTemplateItem> TemplateItems { get; set; } = new List<WeeklyMenuTemplateItem>();
    public virtual ICollection<LunchServiceDayDish> ServiceDayDishes { get; set; } = new List<LunchServiceDayDish>();
}
