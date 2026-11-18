namespace Core.Entities;

public class BoardingIncident
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public int? ClassId { get; set; }
    public virtual Class? Class { get; set; }

    public DateTime Date { get; set; }
    public string IncidentType { get; set; } = "General";
    public string Note { get; set; } = string.Empty;

    public int RecordedByUserId { get; set; }
    public virtual User RecordedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
