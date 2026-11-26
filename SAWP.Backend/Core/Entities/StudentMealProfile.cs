using Core.Enums;

namespace Core.Entities;

public class StudentMealProfile
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public MealType DefaultMealType { get; set; } = MealType.StandardLunch;
    public string? AllergenTags { get; set; }
    public string? AllergyNotes { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
    public virtual User? UpdatedByUser { get; set; }
}
