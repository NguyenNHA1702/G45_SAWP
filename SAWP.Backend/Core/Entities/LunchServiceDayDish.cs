using Core.Enums;

namespace Core.Entities;

public class LunchServiceDayDish
{
    public int Id { get; set; }
    public int LunchServiceDayId { get; set; }
    public virtual LunchServiceDay LunchServiceDay { get; set; } = null!;

    public MealType MealType { get; set; } = MealType.StandardLunch;
    public int DishId { get; set; }
    public virtual Dish Dish { get; set; } = null!;
    public int SortOrder { get; set; }
}
