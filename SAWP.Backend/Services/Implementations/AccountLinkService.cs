using System.Security.Cryptography;
using System.Text;
using Core.Configuration;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.ExternalServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations;

public class AccountLinkService : IAccountLinkService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly AccountLinkOptions _options;
    private readonly IRequestOriginSource _requestOrigins;
    private readonly ILogger<AccountLinkService> _logger;

    public AccountLinkService(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IOptions<AccountLinkOptions> options,
        IRequestOriginSource requestOrigins,
        ILogger<AccountLinkService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _options = options.Value;
        _requestOrigins = requestOrigins;
        _logger = logger;
    }

    /// <summary>
    /// The web client address for a link: the first candidate that is in the allowlist (compared as
    /// scheme://host[:port], case-insensitive), else the first allowlist entry. A candidate outside the list is
    /// never used, so a forged Host / Origin header cannot send a real token to another site.
    /// </summary>
    public static string ChooseLinkOrigin(IEnumerable<string> candidates, IReadOnlyList<string> allowlist)
    {
        var allowed = allowlist.Select(NormalizeOrigin).Where(o => o != null).Select(o => o!).ToList();
        if (allowed.Count == 0)
        {
            throw new InvalidOperationException("AccountLinks:FrontendOrigins is empty.");
        }

        foreach (var candidate in candidates)
        {
            var normalized = NormalizeOrigin(candidate);
            if (normalized != null && allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                return normalized;
            }
        }
        return allowed[0];
    }

    /// <summary>scheme://host[:port] in lower case for an absolute http(s) address, null otherwise.</summary>
    public static string? NormalizeOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }
        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    }

    public async Task<string> IssueAsync(User user, VerificationTokenPurpose purpose, string? newEmail = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var repo = _unitOfWork.Repository<VerificationToken>();

        // A new link replaces the earlier ones of the same purpose (UC-66 A1)
        var earlier = await repo.Query()
            .Where(t => t.UserId == user.Id && t.Purpose == purpose && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (var old in earlier)
        {
            old.ExpiresAt = now;
        }

        var rawToken = NewRawToken();
        await repo.AddAsync(new VerificationToken
        {
            UserId = user.Id,
            Purpose = purpose,
            TokenHash = Hash(rawToken),
            NewEmail = newEmail,
            ExpiresAt = now + Lifetime(purpose),
            CreatedAt = now
        }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return rawToken;
    }

    public async Task<VerificationToken?> FindUsableAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 200)
        {
            return null;
        }

        var hash = Hash(rawToken.Trim());
        var now = DateTime.UtcNow;
        return await _unitOfWork.Repository<VerificationToken>().Query()
            .Include(t => t.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now, ct);
    }

    public Task<bool> SendActivationLinkAsync(User user, CancellationToken ct = default) =>
        SendLinkAsync(user, VerificationTokenPurpose.Activation, ct);

    public Task<bool> SendRecoveryLinkAsync(User user, CancellationToken ct = default) =>
        SendLinkAsync(user, VerificationTokenPurpose.PasswordRecovery, ct);

    public async Task<bool> SendEmailChangeLinkAsync(User user, string newEmail, CancellationToken ct = default)
    {
        var rawToken = await IssueAsync(user, VerificationTokenPurpose.EmailChange, newEmail, ct);
        var origin = ChooseLinkOrigin(_requestOrigins.GetCandidateOrigins(), _options.FrontendOrigins);
        var link = $"{origin}/confirm-email-change?token={Uri.EscapeDataString(rawToken)}";
        var (subject, body) = EmailTemplateRenderer.Render(EmailTemplates.EmailChange, new Dictionary<string, string>
        {
            ["FullName"] = user.FullName,
            ["OldEmail"] = user.Email,
            ["NewEmail"] = newEmail,
            ["Link"] = link,
            ["Hours"] = _options.EmailChangeHours.ToString()
        });

        try
        {
            await _emailService.SendEmailAsync(newEmail, subject, body, true, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not send EmailChange confirmation to user {UserId}", user.Id);
            return false;
        }
    }

    private async Task<bool> SendLinkAsync(User user, VerificationTokenPurpose purpose, CancellationToken ct)
    {
        var rawToken = await IssueAsync(user, purpose, null, ct);
        var page = purpose == VerificationTokenPurpose.Activation ? "activate" : "reset-password";
        var origin = ChooseLinkOrigin(_requestOrigins.GetCandidateOrigins(), _options.FrontendOrigins);
        var link = $"{origin}/{page}?token={Uri.EscapeDataString(rawToken)}";
        var (subject, body) = purpose == VerificationTokenPurpose.Activation
            ? EmailTemplateRenderer.Render(EmailTemplates.Activation, new Dictionary<string, string>
            {
                ["FullName"] = user.FullName,
                ["Username"] = user.Username,
                ["Link"] = link,
                ["Hours"] = _options.ActivationHours.ToString()
            })
            : EmailTemplateRenderer.Render(EmailTemplates.PasswordRecovery, new Dictionary<string, string>
            {
                ["FullName"] = user.FullName,
                ["Link"] = link,
                ["Minutes"] = _options.RecoveryMinutes.ToString()
            });

        try
        {
            await _emailService.SendEmailAsync(user.Email, subject, body, true, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed delivery of one message is only logged (UC-66 E2)
            _logger.LogError(ex, "Could not send the {Purpose} email to user {UserId}", purpose, user.Id);
            return false;
        }
    }

    private TimeSpan Lifetime(VerificationTokenPurpose purpose) => purpose switch
    {
        VerificationTokenPurpose.PasswordRecovery => TimeSpan.FromMinutes(_options.RecoveryMinutes),
        VerificationTokenPurpose.Activation => TimeSpan.FromHours(_options.ActivationHours),
        _ => TimeSpan.FromHours(_options.EmailChangeHours)
    };

    /// <summary>256 random bits, base64url (43 characters).</summary>
    private static string NewRawToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Hex SHA-256 of the raw token; only this is stored.</summary>
    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
