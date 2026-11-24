using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Entities;
using Core.Enums;
using FluentAssertions;
using MockQueryable;
using MockQueryable.Moq;
using Services.Implementations;
using Xunit;

namespace SAWP.UnitTests;

/// <summary>SSAWP-98: ended sessions (BR-03, BR-48).</summary>
public class SessionValidatorTests : TestBase
{
    private SessionValidator WithUser(User user)
    {
        var users = new List<User> { user };
        UserRepoMock.Setup(r => r.Query()).Returns(() => users.BuildMock());
        return new SessionValidator(UnitOfWorkMock.Object);
    }

    [Fact]
    public async Task UTCID01_ActiveAccount_CurrentVersion_IsValid()
    {
        var validator = WithUser(new User { Id = 7, Status = AccountStatus.Active, SessionVersion = 2, MustChangePassword = true });

        var result = await validator.CheckAsync(7, 2);

        result.IsValid.Should().BeTrue();
        result.MustChangePassword.Should().BeTrue();
    }

    [Theory]
    [InlineData(AccountStatus.Active, 3)]        // password changed or roles changed after the token was issued
    [InlineData(AccountStatus.Deactivated, 2)]   // BR-48
    [InlineData(AccountStatus.PendingActivation, 2)]
    public async Task UTCID02_OutdatedVersionOrInactiveAccount_IsInvalid(AccountStatus status, int currentVersion)
    {
        var validator = WithUser(new User { Id = 7, Status = status, SessionVersion = currentVersion });

        (await validator.CheckAsync(7, 2)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID03_UnknownAccount_IsInvalid()
    {
        var validator = WithUser(new User { Id = 7, Status = AccountStatus.Active });

        (await validator.CheckAsync(99, 0)).IsValid.Should().BeFalse();
    }
}
