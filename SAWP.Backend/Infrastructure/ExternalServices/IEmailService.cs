namespace Infrastructure.ExternalServices;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the SMTP server can be reached and accepts the login right now (UC-66 E2). The result is cached
    /// for a short time so that a burst of requests does not open a connection each.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
