namespace Core.Configuration;

/// <summary>Emailed single-use links: activation, password recovery, email change (BR-04, Section 3.2.4).</summary>
public class AccountLinkOptions
{
    public const string SectionName = "AccountLinks";

    /// <summary>
    /// Addresses of the web client (scheme://host[:port]) that emailed links may point to. A link uses the address
    /// the request came from only when it is in this list; otherwise the first entry. A request can name any
    /// host, so an unchecked Host / Origin header would let anyone send real links to their own site
    /// (password-reset poisoning).
    /// </summary>
    public List<string> FrontendOrigins { get; set; } = new();

    public int RecoveryMinutes { get; set; } = 15;
    public int ActivationHours { get; set; } = 72;
    public int EmailChangeHours { get; set; } = 72;

    /// <summary>Link requests allowed per email address entered within <see cref="RequestWindowMinutes"/> (UC-66 E5).</summary>
    public int MaxRequestsPerWindow { get; set; } = 3;
    public int RequestWindowMinutes { get; set; } = 15;
}
