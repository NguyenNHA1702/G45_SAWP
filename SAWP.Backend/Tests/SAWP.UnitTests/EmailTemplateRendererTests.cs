using System;
using System.Collections.Generic;
using FluentAssertions;
using Services.Implementations;
using Xunit;

namespace SAWP.UnitTests;

/// <summary>SSAWP-98: email texts from Services/EmailTemplates (embedded resources).</summary>
public class EmailTemplateRendererTests
{
    /// <summary>The values each template needs (keep in step with EmailTemplates/README.md).</summary>
    public static TheoryData<string, Dictionary<string, string>> AllTemplates => new()
    {
        { EmailTemplates.Activation, new() { ["FullName"] = "Nguyễn Văn A", ["Username"] = "gv01", ["Link"] = "http://web.test/activate?token=abc", ["Hours"] = "72" } },
        { EmailTemplates.PasswordRecovery, new() { ["FullName"] = "Nguyễn Văn A", ["Link"] = "http://web.test/reset-password?token=abc", ["Minutes"] = "15" } },
    };

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void UTCID01_EveryTemplate_RendersSubjectAndBodyWithoutLeftovers(string template, Dictionary<string, string> values)
    {
        var (subject, body) = EmailTemplateRenderer.Render(template, values);

        subject.Should().StartWith("SAWP - ");
        body.Should().Contain("<html").And.Contain("Nguyễn Văn A").And.Contain(values["Link"]);
        body.Should().NotContain("{{").And.NotContain("<!--").And.NotContain("Subject:");
    }

    [Fact]
    public void UTCID02_AllTemplateNames_AreCoveredByTheTestData()
    {
        var covered = new HashSet<string>();
        foreach (var row in AllTemplates)
        {
            covered.Add((string)row[0]);
        }
        covered.Should().BeEquivalentTo(EmailTemplates.All);
    }

    [Fact]
    public void UTCID03_Values_AreHtmlEncoded()
    {
        var (_, body) = EmailTemplateRenderer.Render(EmailTemplates.PasswordRecovery, new Dictionary<string, string>
        {
            ["FullName"] = "<script>alert(1)</script> & \"x\"",
            ["Link"] = "http://web.test/reset-password?token=a&b",
            ["Minutes"] = "15"
        });

        body.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
        body.Should().Contain("token=a&amp;b");
    }

    [Fact]
    public void UTCID04_MissingValue_Throws()
    {
        var act = () => EmailTemplateRenderer.Render(EmailTemplates.Activation, new Dictionary<string, string> { ["FullName"] = "A" });

        act.Should().Throw<InvalidOperationException>().WithMessage("*Username*");
    }

    [Fact]
    public void UTCID05_UnknownTemplate_Throws()
    {
        var act = () => EmailTemplateRenderer.Render("NoSuchTemplate", new Dictionary<string, string>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*not found*");
    }
}
