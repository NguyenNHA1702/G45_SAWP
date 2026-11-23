using Core.Enums;

namespace Core.Entities;

/// <summary>
/// A single-use, time-limited token sent by email as a link (priv ERD row 30, BR-04, BR-63, BR-69).
/// Only the hash of the token is stored.
/// </summary>
public class VerificationToken
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public VerificationTokenPurpose Purpose { get; set; }

    /// <summary>Hash of the random token carried in the link (hex SHA-256).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>The new address, for an email change only.</summary>
    public string? NewEmail { get; set; }

    public DateTime ExpiresAt { get; set; } // UTC
    public DateTime? UsedAt { get; set; } // UTC; null = not used
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
