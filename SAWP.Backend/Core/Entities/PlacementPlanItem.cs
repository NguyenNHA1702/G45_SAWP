namespace Core.Entities;

public class PlacementPlanItem
{
    public int Id { get; set; }

    public int PlacementPlanId { get; set; }
    public virtual PlacementPlan PlacementPlan { get; set; } = null!;

    public int ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public bool IsManualOverride { get; set; } = false;
    public string? Notes { get; set; }
}
