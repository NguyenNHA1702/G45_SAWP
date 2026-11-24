namespace Core.Entities;

/// <summary>
/// A parent / guardian of one or more students (priv ERD row 6). The record exists on its own: it may have no
/// user account, and its contact details belong to the record, not to an account (BR-64, BR-66).
/// </summary>
public class Guardian
{
    public int Id { get; set; }

    /// <summary>The Parent account linked to this record, if any (set only when the account is created, BR-66).</summary>
    public int? UserId { get; set; }
    public virtual User? User { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    /// <summary>Contact email of the guardian; without it the guardian cannot receive an account (BR-64).</summary>
    public string? Email { get; set; }

    public string IdentityCardNumber { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
}
