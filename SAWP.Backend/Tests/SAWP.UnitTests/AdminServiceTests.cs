using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using FluentAssertions;
using Moq;
using Services.Implementations;
using Services.Interfaces;
using Xunit;

namespace SAWP.UnitTests;

/// <summary>SSAWP-98: account creation (UC-53, BR-63), status changes (BR-48) and administrator-assisted reset (UC-60, BR-65).</summary>
public class AdminServiceTests : TestBase
{
    private readonly Mock<IAccountLinkService> _linksMock = new();
    private readonly AdminService _service;
    private readonly List<User> _users = new();

    public AdminServiceTests()
    {
        _linksMock.Setup(l => l.SendActivationLinkAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupRepository(_users);
        SetupRepository(new List<Role>
        {
            new() { Id = 1, Name = "Admin" }, new() { Id = 4, Name = "Teacher" }, new() { Id = 6, Name = "Parent" }
        });
        _service = new AdminService(UnitOfWorkMock.Object, AuditServiceMock.Object, _linksMock.Object);
    }

    private static CreateUserRequestDto NewTeacher(string role = "Teacher") => new()
    {
        Username = "gv01", Email = "gv01@school.vn", FullName = "Giáo viên 01", Roles = new List<string> { role }
    };

    private static User Account(int id, string role, AccountStatus status, string hash = "hash") => new()
    {
        Id = id, Username = "u" + id, Email = $"u{id}@school.vn", FullName = "U", PasswordHash = hash, Status = status,
        UserRoles = new List<UserRole> { new() { Role = new Role { Name = role } } }
    };

    [Fact]
    public async Task UTCID01_CreateUser_StartsPendingActivationWithoutPassword_SendsActivationLink()
    {
        var result = await _service.CreateUserAsync(1, NewTeacher());

        result.Code.Should().Be(SystemMessages.MSG38.Code);
        result.Errors.Should().BeNull();
        var user = _users.Should().ContainSingle().Subject;
        user.Status.Should().Be(AccountStatus.PendingActivation);
        user.PasswordHash.Should().BeEmpty();
        _linksMock.Verify(l => l.SendActivationLinkAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID02_CreateUser_EmailFails_AccountKeptAndMSG59Reported()
    {
        _linksMock.Setup(l => l.SendActivationLinkAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.CreateUserAsync(1, NewTeacher());

        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG38.Code);
        result.Errors.Should().ContainSingle().Which.Should().Be(SystemMessages.MSG59.Content);
        _users.Should().ContainSingle();
    }

    [Fact]
    public async Task UTCID03_CreateUser_ParentRole_RefusedWithMSG01()
    {
        var result = await _service.CreateUserAsync(1, NewTeacher("Parent"));

        result.Code.Should().Be(SystemMessages.MSG01.Code);
        _users.Should().BeEmpty();
        _linksMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID04_ResetPassword_ActiveTeacher_SetsTemporaryPasswordAndEndsSessions()
    {
        var user = Account(7, "Teacher", AccountStatus.Active);
        UserRepoMock.Setup(r => r.GetByIdWithRolesAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _service.ResetUserPasswordAsync(1, 7, "Temp@12345");

        result.Code.Should().Be(SystemMessages.MSG46.Code);
        user.MustChangePassword.Should().BeTrue();
        user.SessionVersion.Should().Be(1);
        BCrypt.Net.BCrypt.Verify("Temp@12345", user.PasswordHash).Should().BeTrue();
    }

    [Theory]
    [InlineData("Admin", AccountStatus.Active)]
    [InlineData("Teacher", AccountStatus.PendingActivation)]
    [InlineData("Teacher", AccountStatus.Deactivated)]
    public async Task UTCID05_ResetPassword_AdministratorOrInactiveAccount_ReturnsMSG10(string role, AccountStatus status)
    {
        var user = Account(7, role, status);
        UserRepoMock.Setup(r => r.GetByIdWithRolesAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _service.ResetUserPasswordAsync(1, 7, "Temp@12345");

        result.Code.Should().Be(SystemMessages.MSG10.Code);
        user.PasswordHash.Should().Be("hash");
        user.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID06_Deactivate_EndsSessions_ReturnsMSG39()
    {
        var user = Account(7, "Teacher", AccountStatus.Active);
        _users.Add(user);

        var result = await _service.UpdateUserStatusAsync(1, 7, false);

        result.Code.Should().Be(SystemMessages.MSG39.Code);
        user.Status.Should().Be(AccountStatus.Deactivated);
        user.SessionVersion.Should().Be(1);
    }

    [Fact]
    public async Task UTCID07_Activate_PendingActivationAccount_RefusedWithMSG09()
    {
        var user = Account(7, "Teacher", AccountStatus.PendingActivation, hash: "");
        _users.Add(user);

        var result = await _service.UpdateUserStatusAsync(1, 7, true);

        result.Code.Should().Be(SystemMessages.MSG09.Code);
        user.Status.Should().Be(AccountStatus.PendingActivation);
    }

    [Theory]
    [InlineData("hash", AccountStatus.Active)]
    [InlineData("", AccountStatus.PendingActivation)]
    public async Task UTCID08_Reactivate_DeactivatedAccount_ActiveOnlyWithPassword(string hash, AccountStatus expected)
    {
        var user = Account(7, "Teacher", AccountStatus.Deactivated, hash);
        _users.Add(user);

        var result = await _service.UpdateUserStatusAsync(1, 7, true);

        result.Success.Should().BeTrue();
        user.Status.Should().Be(expected);
    }

    [Fact]
    public async Task ATT6_UpsertCalendarDay_DefaultDate_FailsWithMSG01()
    {
        var result = await _service.UpsertCalendarDayAsync(1, new UpdateCalendarDayDto { Date = default, IsSchoolDay = false });
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task ATT6_UpsertCalendarDay_NoteExceeds500_FailsWithMSG01()
    {
        var result = await _service.UpsertCalendarDayAsync(1, new UpdateCalendarDayDto
        {
            Date = new DateTime(2026, 11, 24),
            IsSchoolDay = false,
            Note = new string('x', 501)
        });
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task ATT6_UpsertCalendarDay_NonExistentSchoolYear_FailsWithMSG14()
    {
        SetupRepository(new List<SchoolYear>
        {
            new() { Id = 1, YearName = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 5, 31) }
        });

        var result = await _service.UpsertCalendarDayAsync(1, new UpdateCalendarDayDto
        {
            SchoolYearId = 999,
            Date = new DateTime(2026, 11, 24),
            IsSchoolDay = false
        });
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG14.Code);
    }

    [Fact]
    public async Task ATT6_UpsertCalendarDay_DateOutsideSchoolYear_FailsWithMSG01()
    {
        SetupRepository(new List<SchoolYear>
        {
            new() { Id = 1, YearName = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 5, 31) }
        });

        var result = await _service.UpsertCalendarDayAsync(1, new UpdateCalendarDayDto
        {
            SchoolYearId = 1,
            Date = new DateTime(2090, 1, 3),
            IsSchoolDay = false
        });
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }
}
