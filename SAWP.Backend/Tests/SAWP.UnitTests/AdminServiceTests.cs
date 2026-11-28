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

    private static CreateUserRequestDto NewTeacher(string role = "Teacher", bool enableNow = true) => new()
    {
        Username = "gv01", Email = "gv01@school.vn", FullName = "Giáo viên Một", Roles = new List<string> { role }, EnableNow = enableNow
    };

    private static User Account(int id, string role, AccountStatus status, string hash = "hash") => new()
    {
        Id = id, Username = "u" + id, Email = $"u{id}@school.vn", FullName = "U", PasswordHash = hash, Status = status,
        UserRoles = new List<UserRole> { new() { Role = new Role { Name = role } } }
    };

    [Fact]
    public async Task UTCID01_CreateUser_EnableNowTrue_StartsPendingActivationWithoutPassword_SendsActivationLink()
    {
        var result = await _service.CreateUserAsync(1, NewTeacher(enableNow: true));

        result.Code.Should().Be(SystemMessages.MSG38.Code);
        result.Errors.Should().BeNull();
        var user = _users.Should().ContainSingle().Subject;
        user.Status.Should().Be(AccountStatus.PendingActivation);
        user.PasswordHash.Should().BeEmpty();
        _linksMock.Verify(l => l.SendActivationLinkAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID01b_CreateUser_DefaultDisabled_StaysDeactivatedWithoutSendingLink()
    {
        var result = await _service.CreateUserAsync(1, NewTeacher(enableNow: false));

        result.Code.Should().Be(SystemMessages.MSG38.Code);
        var user = _users.Should().ContainSingle().Subject;
        user.Status.Should().Be(AccountStatus.Deactivated);
        user.PasswordHash.Should().BeEmpty();
        _linksMock.Verify(l => l.SendActivationLinkAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID02_CreateUser_EmailFails_AccountKeptAndMSG59Reported()
    {
        _linksMock.Setup(l => l.SendActivationLinkAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.CreateUserAsync(1, NewTeacher(enableNow: true));

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

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task UTCID03b_CreateUser_NotExactlyOneRole_RefusedWithMSG01(int roleCount)
    {
        var request = NewTeacher();
        request.Roles = new List<string> { nameof(UserRoles.Teacher), nameof(UserRoles.Admin) }.Take(roleCount).ToList();

        var result = await _service.CreateUserAsync(1, request);

        result.Code.Should().Be(SystemMessages.MSG01.Code);
        _users.Should().BeEmpty();
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
    public async Task ATT6_UpsertCalendarDay_NonExistentSchoolYear_FailsWithMSG43()
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
        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    [Fact]
    public async Task ATT6_UpsertCalendarDay_NoActiveSchoolYear_FailsWithMSG14()
    {
        SetupRepository(new List<SchoolYear>());

        var result = await _service.UpsertCalendarDayAsync(1, new UpdateCalendarDayDto
        {
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

    [Theory]
    [InlineData("user 1", "Nguyen Van An", "gv01@school.vn", "0912345678")]
    [InlineData("gv01", "Nguyen Van 123", "gv01@school.vn", "0912345678")]
    [InlineData("gv01", "Nguyen <b>An</b>", "gv01@school.vn", "0912345678")]
    [InlineData("gv01", "Nguyen Van An", "gv01@school.vn", "123")]
    [InlineData("gv01", "Nguyen Van An", "invalid-email", "0912345678")]
    public async Task UI04_CreateUser_InvalidFields_RefusedWithMSG01(string username, string fullName, string email, string phone)
    {
        var result = await _service.CreateUserAsync(1, new CreateUserRequestDto
        {
            Username = username,
            FullName = fullName,
            Email = email,
            PhoneNumber = phone,
            Roles = new List<string> { "Teacher" }
        });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        _users.Should().BeEmpty();
    }

    [Fact]
    public async Task UI04_DeleteUser_PendingActivation_DeletesAndLogsAudit()
    {
        var user = Account(266, "Teacher", AccountStatus.PendingActivation, hash: "");
        _users.Add(user);

        var result = await _service.DeleteUserAsync(1, 266);

        result.Success.Should().BeTrue();
        _users.Should().BeEmpty();
        AuditServiceMock.Verify(a => a.LogActionAsync(1, AuditActions.UserDelete, nameof(User), "266", It.IsAny<object?>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UI04_DeleteUser_ActiveAccount_RefusedWithMSG09()
    {
        var user = Account(7, "Teacher", AccountStatus.Active, hash: "hash");
        _users.Add(user);

        var result = await _service.DeleteUserAsync(1, 7);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
        _users.Should().ContainSingle();
    }

    [Fact]
    public async Task UI04_ResendActivationLink_PendingActivation_SendsLink()
    {
        var user = Account(266, "Teacher", AccountStatus.PendingActivation, hash: "");
        _users.Add(user);

        var result = await _service.ResendActivationLinkAsync(1, 266);

        result.Success.Should().BeTrue();
        _linksMock.Verify(l => l.SendActivationLinkAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UI04_ResendActivationLink_ActiveAccount_RefusedWithMSG09()
    {
        var user = Account(7, "Teacher", AccountStatus.Active, hash: "hash");
        _users.Add(user);

        var result = await _service.ResendActivationLinkAsync(1, 7);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
        _linksMock.Verify(l => l.SendActivationLinkAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task N08_DeleteUser_PendingActivationAdmin_DeletesDirectlyWithoutDemotingRole()
    {
        var adminDraft = Account(300, "Admin", AccountStatus.PendingActivation, hash: "");
        _users.Add(adminDraft);

        var result = await _service.DeleteUserAsync(1, 300);

        result.Success.Should().BeTrue();
        _users.Should().BeEmpty();
    }

    [Fact]
    public async Task BUG_AUTH_03_CreateUser_AllowsReusingEmailOfDeactivatedAccount()
    {
        var oldUser = Account(90, "Teacher", AccountStatus.Deactivated, hash: "hash");
        oldUser.Email = "reused@school.vn";
        _users.Add(oldUser);

        var req = NewTeacher(enableNow: true);
        req.Username = "gv_new";
        req.Email = "reused@school.vn";

        var result = await _service.CreateUserAsync(1, req);

        result.Success.Should().BeTrue();
        _users.Should().HaveCount(2);
    }

    [Fact]
    public async Task BUG_AUTH_04_UpdateUserRoles_ActiveAccountWithData_ReturnsMSG64()
    {
        var user = Account(77, "Teacher", AccountStatus.Active, hash: "hash");
        _users.Add(user);

        var result = await _service.UpdateUserRolesAsync(1, 77, new UpdateUserRolesRequestDto { Roles = new List<string> { "Admin" } });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG64.Code);
    }

    [Fact]
    public async Task BUG_AUTH_04_UpdateUserRoles_SelfChange_ReturnsMSG10()
    {
        var admin = Account(1, "Admin", AccountStatus.Active, hash: "hash");
        _users.Add(admin);

        var result = await _service.UpdateUserRolesAsync(1, 1, new UpdateUserRolesRequestDto { Roles = new List<string> { "Admin", "Teacher" } });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task GAP_05_UC54_57_Permissions_ProfileLink_ReferenceItem_AndNotificationTemplate()
    {
        SetupRepository(new List<Permission> { new() { Id = 1, Code = "STUDENTS_VIEW", Name = "Xem HS", Module = "HS" } });
        var rolePerms = new List<RolePermission>();
        SetupRepository(rolePerms);

        var permRes = await _service.UpdateRolePermissionsAsync(1, 4, new UpdateRolePermissionsDto
        {
            GrantedPermissionCodes = new List<string> { "STUDENTS_VIEW" }
        });
        permRes.Success.Should().BeTrue();
        rolePerms.Should().ContainSingle(rp => rp.RoleId == 4 && rp.PermissionId == 1 && rp.IsGranted);

        var user = Account(88, "Parent", AccountStatus.Active);
        _users.Add(user);
        var guardian = new Guardian { Id = 5, FullName = "PH Test", PhoneNumber = "0909123123" };
        SetupRepository(new List<Guardian> { guardian });
        var linkRes = await _service.UpdateUserProfileLinkAsync(1, 88, new UpdateUserProfileLinkDto { ProfileType = "Guardian", ProfileId = 5 });
        linkRes.Success.Should().BeTrue();
        guardian.UserId.Should().Be(88);

        var refItems = new List<ReferenceItem>();
        SetupRepository(refItems);
        var refRes = await _service.UpsertReferenceItemAsync(1, null, new UpsertReferenceItemDto
        {
            Category = "DocumentType", Code = "DOC_NEW", Label = "Giấy mới", SortOrder = 1, IsActive = true
        });
        refRes.Success.Should().BeTrue();
        refItems.Should().ContainSingle(r => r.Code == "DOC_NEW");

        var tpl = new NotificationTemplate { Id = 1, Code = "LEAVE_APPROVED", TitleTemplate = "Old", BodyTemplate = "OldBody", IsActive = true };
        SetupRepository(new List<NotificationTemplate> { tpl });
        var tplRes = await _service.UpdateNotificationTemplateAsync(1, 1, new UpdateNotificationTemplateDto
        {
            TitleTemplate = "New Title", BodyTemplate = "New Body", IsActive = true
        });
        tplRes.Success.Should().BeTrue();
        tpl.TitleTemplate.Should().Be("New Title");
    }

    [Fact]
    public async Task GAP_03_BroadcastNotification_SendsToTargetRoleUsers()
    {
        var users = new List<User>
        {
            new() { Id = 20, Status = AccountStatus.Active, UserRoles = new List<UserRole> { new() { Role = new Role { Name = "Parent" } } } },
            new() { Id = 21, Status = AccountStatus.Active, UserRoles = new List<UserRole> { new() { Role = new Role { Name = "Teacher" } } } },
        };
        SetupRepository(users);
        var notifs = new List<Notification>();
        SetupRepository(notifs);

        var notifService = new NotificationService(UnitOfWorkMock.Object, AuditServiceMock.Object);
        var res = await notifService.BroadcastAsync(10, new BroadcastNotificationRequestDto
        {
            TargetScope = "Role",
            TargetRole = "Parent",
            Title = "Thông báo họp phụ huynh",
            Content = "Kính mời quý phụ huynh tham dự họp đầu năm học."
        });

        res.Success.Should().BeTrue();
        res.Data!.RecipientCount.Should().Be(1);
        notifs.Should().ContainSingle(n => n.UserId == 20 && n.Type == "SchoolBroadcast");
    }
}
