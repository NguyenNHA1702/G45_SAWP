using Core.Configuration;
using FluentAssertions;
using Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SAWP.UnitTests;

public class EmailServiceTests
{
    private static EmailService CreateService(SmtpOptions options) =>
        new(Options.Create(options), NullLogger<EmailService>.Instance);

    [Fact]
    public async Task SendEmailAsync_WithoutHost_ThrowsConfigurationError()
    {
        var service = CreateService(new SmtpOptions { FromAddress = "no-reply@sawp.local" });

        var act = () => service.SendEmailAsync("phuhuynh@example.com", "Test", "Body");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Smtp:Host*");
    }

    [Fact]
    public async Task SendEmailAsync_WithoutFromAddress_ThrowsConfigurationError()
    {
        var service = CreateService(new SmtpOptions { Host = "localhost", Port = 1025, Security = SmtpSecurity.None });

        var act = () => service.SendEmailAsync("phuhuynh@example.com", "Test", "Body");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Smtp:FromAddress*");
    }
}
