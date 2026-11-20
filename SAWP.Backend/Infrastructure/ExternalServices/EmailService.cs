using Core.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.ExternalServices;

public class EmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException(
                $"SMTP is not configured: set {SmtpOptions.SectionName}:Host and {SmtpOptions.SectionName}:FromAddress.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder();
        if (isHtml)
        {
            bodyBuilder.HtmlBody = body;
        }
        else
        {
            bodyBuilder.TextBody = body;
        }

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient { Timeout = _options.TimeoutSeconds * 1000 };
        await client.ConnectAsync(_options.Host, _options.Port, ToSecureSocketOptions(_options.Security), cancellationToken);
        if (!string.IsNullOrEmpty(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Sent email '{Subject}' to {Recipient} via {Host}:{Port}", subject, toEmail, _options.Host, _options.Port);
    }

    private static SecureSocketOptions ToSecureSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.Auto
    };
}
