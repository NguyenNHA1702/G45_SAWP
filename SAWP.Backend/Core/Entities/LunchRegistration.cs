using Core.Enums;

namespace Core.Entities;

public class LunchRegistration
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public DateTime Date { get; set; }
    public MealType MealType { get; set; } = MealType.StandardLunch;
    public bool IsActive { get; set; } = true;
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
