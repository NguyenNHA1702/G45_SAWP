using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using FluentAssertions;
using Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Services.Implementations;
using Xunit;

namespace SAWP.UnitTests;

/// <summary>SSAWP-98: emailed single-use links (BR-04, Section 3.2.4).</summary>
public class AccountLinkServiceTests : TestBase
{
    private readonly Mock<IEmailService> _emailMock = new();
    private readonly Mock<IRequestOriginSource> _origins = new();
    private readonly List<VerificationToken> _tokens = new();
    private readonly AccountLinkService _service;
    private readonly User _user = new() { Id = 7, Username = "gv07", FullName = "Giáo viên", Email = "gv07@school.vn" };

    public AccountLinkServiceTests()
    {
        SetupRepository(_tokens);
        _origins.Setup(o => o.GetCandidateOrigins()).Returns(System.Array.Empty<string>());
        _service = new AccountLinkService(UnitOfWorkMock.Object, _emailMock.Object,
            Options.Create(new AccountLinkOptions { FrontendOrigins = new() { "http://web.test/", "https://sawp.school.vn" } }),
            _origins.Object, NullLogger<AccountLinkService>.Instance);
    }

    [Fact]
    public async Task UTCID01_Issue_StoresOnlyTheHash_WithPurposeLifetime()
    {
        var raw = await _service.IssueAsync(_user, VerificationTokenPurpose.PasswordRecovery);

        var stored = _tokens.Should().ContainSingle().Subject;
        raw.Length.Should().BeGreaterThanOrEqualTo(43);
        stored.TokenHash.Should().NotBe(raw).And.Be(AccountLinkService.Hash(raw));
        stored.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UTCID02_Issue_EndsEarlierUnusedLinkOfSamePurpose()
    {
        var first = await _service.IssueAsync(_user, VerificationTokenPurpose.Activation);
        var second = await _service.IssueAsync(_user, VerificationTokenPurpose.Activation);

        (await _service.FindUsableAsync(first)).Should().BeNull();
        (await _service.FindUsableAsync(second)).Should().NotBeNull();
    }

    [Fact]
    public async Task UTCID03_FindUsable_UsedOrUnknownToken_ReturnsNull()
    {
        var raw = await _service.IssueAsync(_user, VerificationTokenPurpose.PasswordRecovery);
        _tokens.Single().UsedAt = DateTime.UtcNow;

        (await _service.FindUsableAsync(raw)).Should().BeNull();
        (await _service.FindUsableAsync("unknown")).Should().BeNull();
    }

    [Fact]
    public async Task UTCID04_SendActivationLink_EmailsLinkToActivatePage()
    {
        string? body = null;
        _emailMock.Setup(e => e.SendEmailAsync("gv07@school.vn", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .Callback<string, string, string, bool, CancellationToken>((_, _, b, _, _) => body = b)
            .Returns(Task.CompletedTask);

        var sent = await _service.SendActivationLinkAsync(_user);

        sent.Should().BeTrue();
        body.Should().Contain("http://web.test/activate?token=");
        _tokens.Single().Purpose.Should().Be(VerificationTokenPurpose.Activation);
    }

    [Fact]
    public async Task UTCID05_SendRecoveryLink_SmtpFails_ReturnsFalse()
    {
        _emailMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        (await _service.SendRecoveryLinkAsync(_user)).Should().BeFalse();
    }

    // ---- Link address: the request's origin only when it is in AccountLinks:FrontendOrigins ----

    private static readonly string[] Allowlist = { "https://sawp.school.vn", "https://sawp.truong.vn/", "http://localhost:5173" };

    [Theory]
    [InlineData(new[] { "https://SAWP.truong.vn" }, "https://sawp.truong.vn")]                    // listed, any case
    [InlineData(new[] { "http://localhost:5173", "http://localhost:5299" }, "http://localhost:5173")] // Origin first (Vite proxy)
    [InlineData(new[] { "http://localhost:5299", "https://sawp.truong.vn" }, "https://sawp.truong.vn")] // first listed candidate wins
    public void UTCID06_ChooseLinkOrigin_ListedCandidate_IsUsed(string[] candidates, string expected)
    {
        AccountLinkService.ChooseLinkOrigin(candidates, Allowlist).Should().Be(expected);
    }

    public static TheoryData<string[]> UnlistedCandidates => new()
    {
        new[] { "https://evil.example" },                  // forged Host / Origin
        new[] { "https://sawp.school.vn.evil.example" },   // look-alike
        new[] { "http://sawp.school.vn" },                 // wrong scheme
        new[] { "javascript:alert(1)", "not a url" },
        System.Array.Empty<string>(),                      // no request (e.g. background work)
    };

    [Theory]
    [MemberData(nameof(UnlistedCandidates))]
    public void UTCID07_ChooseLinkOrigin_UnlistedOrMissing_FallsBackToFirstEntry(string[] candidates)
    {
        AccountLinkService.ChooseLinkOrigin(candidates, Allowlist).Should().Be("https://sawp.school.vn");
    }

    [Fact]
    public async Task UTCID08_SendRecoveryLink_ForgedOrigin_LinkPointsToListedAddress()
    {
        _origins.Setup(o => o.GetCandidateOrigins()).Returns(new[] { "https://evil.example" });
        string? body = null;
        _emailMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .Callback<string, string, string, bool, CancellationToken>((_, _, b, _, _) => body = b)
            .Returns(Task.CompletedTask);

        await _service.SendRecoveryLinkAsync(_user);

        body.Should().Contain("http://web.test/reset-password?token=").And.NotContain("evil.example");
    }

    [Fact]
    public async Task UTCID09_SendRecoveryLink_ListedOrigin_LinkUsesIt()
    {
        _origins.Setup(o => o.GetCandidateOrigins()).Returns(new[] { "https://sawp.school.vn" });
        string? body = null;
        _emailMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .Callback<string, string, string, bool, CancellationToken>((_, _, b, _, _) => body = b)
            .Returns(Task.CompletedTask);

        await _service.SendRecoveryLinkAsync(_user);

        body.Should().Contain("https://sawp.school.vn/reset-password?token=");
    }
}
