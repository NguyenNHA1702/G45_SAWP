namespace Services.Interfaces;

/// <summary>Result of checking a login token against the account's current state.</summary>
public record SessionCheckResult(bool IsValid, bool MustChangePassword);

/// <summary>
/// Checks on every authenticated request that the account is still Active and that the token's session version is
/// current, so deactivation and password changes end existing sessions (BR-03, BR-48).
/// </summary>
public interface ISessionValidator
{
    Task<SessionCheckResult> CheckAsync(int userId, int sessionVersion, CancellationToken ct = default);
}
