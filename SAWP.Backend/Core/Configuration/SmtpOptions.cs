namespace Core.Configuration;

// How the connection to the SMTP server is encrypted.
public enum SmtpSecurity
{
    None,          // No encryption (MailHog in development, port 1025)
    StartTls,      // Upgrade to TLS after connecting (port 587, standard for real servers)
    SslOnConnect,  // TLS from the start (port 465)
    Auto           // Let MailKit choose based on the port
}

public class SmtpOptions
{
    public const string SectionName = "Smtp";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;
    // Leave empty when the server needs no login (MailHog).
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "SAWP";
    public int TimeoutSeconds { get; set; } = 30;
}
