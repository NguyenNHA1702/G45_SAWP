using System.Net.Mail;
using Core.Entities;

namespace Services.Implementations;

/// <summary>How a guardian from an import row or a form relates to the existing guardian records.</summary>
public enum GuardianMatchKind
{
    /// <summary>No guardian given.</summary>
    None,
    /// <summary>No record matches: a new guardian record is created.</summary>
    New,
    /// <summary>One record matches exactly by identity card number or guardian-record email: it is reused.</summary>
    Reused,
    /// <summary>The identity card number and the email point to different records, or contradict the matched record.</summary>
    Conflict
}

public sealed record GuardianMatch(GuardianMatchKind Kind, Guardian? Record, string? Note)
{
    /// <summary>A guardian can receive an account only with an email address and no account yet (BR-64).</summary>
    public static bool IsEligibleForAccount(string? email, Guardian? record) =>
        record?.UserId == null
        && !string.IsNullOrWhiteSpace(string.IsNullOrWhiteSpace(record?.Email) ? email : record.Email);
}

/// <summary>
/// UC-09 A3, BR-66: an existing guardian record is reused only when its identity card number or the email address on
/// the guardian record matches exactly. Phone numbers and account emails are never used to match.
/// </summary>
public static class GuardianMatcher
{
    public static string NormalizeIdentityCard(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    public static string NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    public static bool IsValidEmail(string? value)
    {
        var email = value?.Trim() ?? string.Empty;
        return email.Length is > 0 and <= 256 && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;
    }

    /// <summary>Matches against <paramref name="candidates"/> (existing records plus records created earlier in the same import).</summary>
    public static GuardianMatch Match(string? fullName, string? email, string? identityCard, IEnumerable<Guardian> candidates)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return new GuardianMatch(GuardianMatchKind.None, null, null);
        }

        var id = NormalizeIdentityCard(identityCard);
        var mail = NormalizeEmail(email);
        var list = candidates as IReadOnlyCollection<Guardian> ?? candidates.ToList();

        var byId = id.Length == 0 ? new List<Guardian>() : list.Where(g => NormalizeIdentityCard(g.IdentityCardNumber) == id).ToList();
        var byEmail = mail.Length == 0 ? new List<Guardian>() : list.Where(g => NormalizeEmail(g.Email) == mail).ToList();

        if (byId.Count > 1 || byEmail.Count > 1)
        {
            return new GuardianMatch(GuardianMatchKind.Conflict, null,
                "Có nhiều hồ sơ người giám hộ trùng CCCD hoặc email; cần kiểm tra lại dữ liệu.");
        }

        var idRecord = byId.FirstOrDefault();
        var emailRecord = byEmail.FirstOrDefault();
        if (idRecord != null && emailRecord != null && !ReferenceEquals(idRecord, emailRecord))
        {
            return new GuardianMatch(GuardianMatchKind.Conflict, null,
                "CCCD và email thuộc hai hồ sơ người giám hộ khác nhau.");
        }

        var record = idRecord ?? emailRecord;
        if (record == null)
        {
            return new GuardianMatch(GuardianMatchKind.New, null, null);
        }

        if (idRecord != null && mail.Length > 0 && NormalizeEmail(record.Email) is { Length: > 0 } recordMail && recordMail != mail)
        {
            return new GuardianMatch(GuardianMatchKind.Conflict, record,
                "Hồ sơ người giám hộ có cùng CCCD nhưng email khác.");
        }
        if (emailRecord != null && id.Length > 0 && NormalizeIdentityCard(record.IdentityCardNumber) is { Length: > 0 } recordId && recordId != id)
        {
            return new GuardianMatch(GuardianMatchKind.Conflict, record,
                "Hồ sơ người giám hộ có cùng email nhưng CCCD khác.");
        }

        var note = string.Equals(record.FullName?.Trim(), fullName.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Dùng hồ sơ có sẵn \"{record.FullName}\" (họ tên trong dữ liệu khác).";
        return new GuardianMatch(GuardianMatchKind.Reused, record, note);
    }

    /// <summary>Fills only the empty contact fields of a reused record; existing values are never overwritten.</summary>
    public static bool FillEmptyFields(Guardian record, string? phone, string? email, string? identityCard)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(record.PhoneNumber) && !string.IsNullOrWhiteSpace(phone))
        {
            record.PhoneNumber = phone.Trim();
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(record.Email) && !string.IsNullOrWhiteSpace(email))
        {
            record.Email = email.Trim();
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(record.IdentityCardNumber) && !string.IsNullOrWhiteSpace(identityCard))
        {
            record.IdentityCardNumber = identityCard.Trim();
            changed = true;
        }
        return changed;
    }
}
