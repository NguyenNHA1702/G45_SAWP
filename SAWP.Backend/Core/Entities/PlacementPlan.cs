namespace Core.Entities;

public class PlacementPlan
{
    public int Id { get; set; }

    public int SchoolYearId { get; set; }
    public virtual SchoolYear SchoolYear { get; set; } = null!;

    public int Grade { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int Version { get; set; } = 1;

    public int TargetClassCount { get; set; }
    public int TargetCapacity { get; set; } = 35;

    /// <summary>
    /// Status: "Draft", "Submitted", "Approved", "Rejected"
    /// </summary>
    public string Status { get; set; } = "Draft";

    public int CreatedByUserId { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SubmittedAt { get; set; }

    public int? ReviewedByUserId { get; set; }
    public virtual User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }

    public virtual ICollection<PlacementPlanItem> Items { get; set; } = new List<PlacementPlanItem>();
}
