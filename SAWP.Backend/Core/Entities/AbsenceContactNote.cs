using Core.Enums;

namespace Core.Entities;

/// <summary>
/// OVR-10 (UC-23 A1, BR-62): a contact with the family about one unexcused absence. A saved note is never
/// edited or deleted; a wrong note is marked superseded (who, when, why) and no longer counts as a contact.
/// Visible only to school staff with access to the class, never to Parents / Guardians.
/// </summary>
public class AbsenceContactNote
{
    public int Id { get; set; }

    public int AttendanceRecordId { get; set; }
    public virtual AttendanceRecord AttendanceRecord { get; set; } = null!;

    /// <summary>When the family was contacted (UTC).</summary>
    public DateTime ContactedAt { get; set; }
    public ContactChannel Channel { get; set; }
    public ContactOutcome Outcome { get; set; }
    public string Note { get; set; } = string.Empty;   // max 500

    public int CreatedByUserId { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Superseded: all three are set together, or all are null (note in effect)
    public DateTime? SupersededAt { get; set; }        // UTC
    public int? SupersededByUserId { get; set; }
    public virtual User? SupersededByUser { get; set; }
    public string? SupersedeReason { get; set; }       // 5..500
}
