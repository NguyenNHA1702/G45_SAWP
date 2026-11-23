using Core.Enums;

namespace Core.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>Login email: required and unique (BR-64). Not the guardian record's contact email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>When <see cref="Email"/> was verified through an emailed link (UTC); null = not verified yet.</summary>
    public DateTime? EmailVerifiedAt { get; set; }

    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    /// <summary>Only Active accounts can sign in (BR-01, BR-63).</summary>
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual StaffProfile? StaffProfile { get; set; }
    public virtual Guardian? Guardian { get; set; }
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public virtual ICollection<VerificationToken> VerificationTokens { get; set; } = new List<VerificationToken>();
}
