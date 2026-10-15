using MailKit.Net.Smtp;
using MimeKit;

namespace Infrastructure.ExternalServices;

public class EmailService : IEmailService
{
    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.To.Add(new MailboxAddress(toEmail, toEmail));
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
        await Task.CompletedTask;
    }
}
