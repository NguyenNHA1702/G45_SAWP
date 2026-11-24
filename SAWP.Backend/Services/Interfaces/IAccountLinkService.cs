using Core.Entities;
using Core.Enums;

namespace Services.Interfaces;

/// <summary>
/// Emailed single-use links for activation, password recovery and email change (BR-04, BR-63, BR-69, Section 3.2.4).
/// The token travels only in the link; the database keeps its SHA-256 hash.
/// </summary>
public interface IAccountLinkService
{
    /// <summary>Creates a token for the account, ends the account's earlier unused tokens of the same purpose (UC-66 A1) and saves.</summary>
    Task<string> IssueAsync(User user, VerificationTokenPurpose purpose, string? newEmail = null, CancellationToken ct = default);

    /// <summary>The unused, unexpired token for the raw value from a link, with its user; null otherwise.</summary>
    Task<VerificationToken?> FindUsableAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Issues an activation link and emails it. False when the email could not be sent (logged).</summary>
    Task<bool> SendActivationLinkAsync(User user, CancellationToken ct = default);

    /// <summary>Issues a password recovery link and emails it. False when the email could not be sent (logged).</summary>
    Task<bool> SendRecoveryLinkAsync(User user, CancellationToken ct = default);
}
