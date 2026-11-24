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
using Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using MockQueryable.Moq;
using Moq;
using Services.Implementations;
using Services.Interfaces;
using Xunit;

namespace SAWP.UnitTests;

/// <summary>SSAWP-153: UC-67 Request Parent Accounts, UC-68 Review Account Requests (BR-63, BR-66, BR-67, BR-68).</summary>
public class AccountRequestServiceTests : TestBase
{
    private const int OfficeId = 3, AdminId = 1;

    private readonly Mock<IAccountLinkService> _links = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly List<User> _users = new();
    private readonly List<Guardian> _guardians = new();
    private readonly List<AccountRequest> _requests = new();
    private readonly List<Notification> _notifications = new();
    private readonly AccountRequestService _service;

    private static readonly Role AdminRole = new() { Id = 1, Name = "Admin" };
    private static readonly Role TeacherRole = new() { Id = 4, Name = "Teacher" };
    private static readonly Role ParentRole = new() { Id = 6, Name = "Parent" };

    public AccountRequestServiceTests()
    {
        _users.Add(new User { Id = AdminId, Username = "admin", Email = "admin@school.vn", FullName = "Admin", Status = AccountStatus.Active, UserRoles = { new() { Role = AdminRole } } });
        _users.Add(new User { Id = OfficeId, Username = "office1", Email = "office@school.vn", FullName = "Giáo vụ", Status = AccountStatus.Active, UserRoles = { new() { Role = new Role { Name = "OfficeStaff" } } } });
        _users.Add(new User { Id = 4, Username = "teacher1", Email = "teacher@school.vn", FullName = "GV", Status = AccountStatus.Active, UserRoles = { new() { Role = TeacherRole } } });
        _users.Add(new User { Id = 5, Username = "ph_old", Email = "used@example.com", FullName = "PH cũ", Status = AccountStatus.Active, UserRoles = { new() { Role = ParentRole } } });

        SetupRepository(_users);
        UserRepoMock.Setup(r => r.Query()).Returns(() => _users.BuildMock());
        UserRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => _users.FirstOrDefault(u => u.Id == id));
        SetupRepository(_guardians);
        SetupRepository(_requests);
        // Items live in their request's collection, as EF loads them
        var items = SetupRepository(new List<AccountRequestItem>());
        items.Setup(r => r.Query()).Returns(() => _requests.SelectMany(r => r.Items).ToList().BuildMock());
        SetupRepository(_notifications);
        SetupRepository(new List<Role> { AdminRole, TeacherRole, ParentRole });
        SetupRepository(new List<UserRole>());

        _email.Setup(e => e.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _links.Setup(l => l.SendActivationLinkAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _service = new AccountRequestService(UnitOfWorkMock.Object, AuditServiceMock.Object, _links.Object, _email.Object,
            NullLogger<AccountRequestService>.Instance);
    }

    private Guardian AddGuardian(int id, string? email, int? userId = null)
    {
        var g = new Guardian { Id = id, FullName = $"Phụ huynh {id}", Email = email, UserId = userId, PhoneNumber = "09000000" + id };
        _guardians.Add(g);
        return g;
    }

    private AccountRequest AddSubmittedRequest(int id, params Guardian[] guardians)
    {
        var request = new AccountRequest
        {
            Id = id, Title = "Đầu năm", Reason = AccountRequestReason.NewSchoolYearIntake, RequestedByUserId = OfficeId,
            RequestedByUser = _users.First(u => u.Id == OfficeId), Status = AccountRequestStatus.Submitted, SubmittedAt = DateTime.UtcNow
        };
        var itemId = id * 100;
        foreach (var g in guardians)
        {
            request.Items.Add(new AccountRequestItem
            {
                Id = ++itemId, AccountRequest = request, AccountRequestId = id, GuardianId = g.Id, Guardian = g,
                Email = g.Email, Status = AccountRequestItemStatus.Waiting
            });
        }
        _requests.Add(request);
        return request;
    }

    private SaveAccountRequestDto Submit(params int[] guardianIds) => new()
    {
        Title = "Yêu cầu cấp tài khoản", Reason = "NewSchoolYearIntake", GuardianIds = guardianIds.ToList(), Submit = true
    };

    [Fact]
    public async Task UTCID01_CheckRows_ReportsEveryRule()
    {
        AddGuardian(10, "ok@example.com");
        AddGuardian(11, "x@example.com", userId: 5);
        AddGuardian(12, null);
        AddGuardian(13, "khong-hop-le");
        AddGuardian(14, "TEACHER@school.vn");
        AddGuardian(15, "used@example.com");
        AddGuardian(16, "OK@example.com");
        var inOpen = AddGuardian(17, "open@example.com");
        AddSubmittedRequest(1, inOpen);

        var rows = (await _service.CheckRowsAsync(new List<int> { 10, 11, 12, 13, 14, 15, 16, 17 })).Data!;

        rows.Select(r => r.CheckResult).Should().Equal(
            "Passed", "AlreadyHasAccount", "MissingEmail", "InvalidEmail", "EmailOfStaffAccount", "EmailInUse", "DuplicateInRequest", "InOpenRequest");
        rows.Single(r => r.GuardianId == 14).Message.Should().Contain("BR-68");
    }

    [Fact]
    public async Task UTCID02_Submit_LeavesOutFailingRows_NotifiesAdministrators_MSG60()
    {
        AddGuardian(10, "ok@example.com");
        AddGuardian(12, null);

        var result = await _service.CreateRequestAsync(OfficeId, Submit(10, 12));

        result.Code.Should().Be(SystemMessages.MSG60.Code);
        result.Data!.ExcludedRows.Should().ContainSingle(e => e.GuardianId == 12 && e.CheckResult == "MissingEmail");
        var saved = _requests.Single();
        saved.Status.Should().Be(AccountRequestStatus.Submitted);
        saved.Items.Should().ContainSingle(i => i.GuardianId == 10 && i.Status == AccountRequestItemStatus.Waiting && i.Email == "ok@example.com");
        _notifications.Should().ContainSingle(n => n.UserId == AdminId);
    }

    [Fact]
    public async Task UTCID03_Submit_NoValidRow_ReturnsMSG01_NothingSaved()
    {
        AddGuardian(12, null);

        var result = await _service.CreateRequestAsync(OfficeId, Submit(12));

        result.Code.Should().Be(SystemMessages.MSG01.Code);
        _requests.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID04_SaveDraft_KeepsAllRows_NoNotification()
    {
        AddGuardian(10, "ok@example.com");
        AddGuardian(12, null);

        var draft = Submit(10, 12);
        draft.Submit = false;
        var result = await _service.CreateRequestAsync(OfficeId, draft);

        result.Code.Should().Be(SystemMessages.MSG08.Code);
        _requests.Single().Status.Should().Be(AccountRequestStatus.Draft);
        _requests.Single().Items.Should().HaveCount(2);
        _notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID05_ApproveAllValid_CreatesPendingParentAccountsLinkedToRecords_MSG61()
    {
        var g1 = AddGuardian(10, "a@example.com");
        var g2 = AddGuardian(11, "b@example.com");
        var request = AddSubmittedRequest(1, g1, g2);

        var result = await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { ApproveAllValid = true });

        result.Code.Should().Be(SystemMessages.MSG61.Code);
        result.Data!.AccountsCreated.Should().Be(2);
        var created = _users.Single(u => u.Email == "a@example.com");
        created.Username.Should().Be("a@example.com", "the guardian's email is the login");
        created.Status.Should().Be(AccountStatus.PendingActivation);
        created.PasswordHash.Should().BeEmpty("BR-63: no password until activation");
        created.UserRoles.Should().ContainSingle(r => r.RoleId == ParentRole.Id);
        g1.User.Should().BeSameAs(created, "BR-66: linked to its guardian record at creation");
        _links.Verify(l => l.SendActivationLinkAsync(created, It.IsAny<CancellationToken>()), Times.Once);
        request.Status.Should().Be(AccountRequestStatus.Completed);
        _notifications.Should().ContainSingle(n => n.UserId == OfficeId);
    }

    [Fact]
    public async Task UTCID06_ApproveOneRejectOne_PartlyCompleted_RejectNeedsReason()
    {
        var g1 = AddGuardian(10, "a@example.com");
        var g2 = AddGuardian(11, "b@example.com");
        var request = AddSubmittedRequest(1, g1, g2);
        var (row1, row2) = (request.Items.First(), request.Items.Last());

        var noReason = await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { RejectItemIds = { row2.Id } });
        noReason.Code.Should().Be(SystemMessages.MSG01.Code);

        await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { ApproveItemIds = { row1.Id }, RejectItemIds = { row2.Id }, RejectReason = "Sai email" });

        row1.Status.Should().Be(AccountRequestItemStatus.AccountCreated);
        row2.Status.Should().Be(AccountRequestItemStatus.Rejected);
        row2.Reason.Should().Be("Sai email");
        request.Status.Should().Be(AccountRequestStatus.PartlyCompleted);
    }

    [Fact]
    public async Task UTCID07_DecidePartOfTheRows_RequestStaysInReview()
    {
        var request = AddSubmittedRequest(1, AddGuardian(10, "a@example.com"), AddGuardian(11, "b@example.com"));

        await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { ApproveItemIds = { request.Items.First().Id } });

        request.Status.Should().Be(AccountRequestStatus.InReview);
        request.Items.Last().Status.Should().Be(AccountRequestItemStatus.Waiting);
        _notifications.Should().BeEmpty("Office Staff are told when the request is finished");
    }

    [Fact]
    public async Task UTCID08_EmailServiceDown_AccountsCreated_RowsEmailFailed_MSG59()
    {
        _email.Setup(e => e.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var request = AddSubmittedRequest(1, AddGuardian(10, "a@example.com"));

        var result = await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { ApproveAllValid = true });

        result.Code.Should().Be(SystemMessages.MSG59.Code);
        _users.Should().Contain(u => u.Email == "a@example.com");
        request.Items.Single().Status.Should().Be(AccountRequestItemStatus.EmailFailed);
        _links.Verify(l => l.SendActivationLinkAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID09_OneRowFailsItsCheckAtReview_OtherRowsContinue()
    {
        var late = AddGuardian(11, "late@example.com");
        var request = AddSubmittedRequest(1, AddGuardian(10, "a@example.com"), late);
        // The address was taken by another account after the request was sent
        _users.Add(new User { Id = 9, Username = "x", Email = "late@example.com", FullName = "X", UserRoles = { new() { Role = ParentRole } } });

        var result = await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { ApproveAllValid = true });

        result.Data!.AccountsCreated.Should().Be(1);
        request.Items.Single(i => i.GuardianId == 10).Status.Should().Be(AccountRequestItemStatus.AccountCreated);
        request.Items.Single(i => i.GuardianId == 11).Status.Should().Be(AccountRequestItemStatus.Waiting);
        request.Status.Should().Be(AccountRequestStatus.InReview);

        var explicitApprove = await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { ApproveItemIds = { request.Items.Single(i => i.GuardianId == 11).Id } });
        explicitApprove.Data!.NotApproved.Should().ContainSingle(n => n.CheckResult == "EmailInUse");
    }

    [Fact]
    public async Task UTCID10_RejectWholeRequest_Rejected()
    {
        var request = AddSubmittedRequest(1, AddGuardian(10, "a@example.com"), AddGuardian(11, "b@example.com"));

        await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { RejectAll = true, RejectReason = "Trùng đợt trước" });

        request.Status.Should().Be(AccountRequestStatus.Rejected);
        request.Items.Should().OnlyContain(i => i.Status == AccountRequestItemStatus.Rejected && i.Reason == "Trùng đợt trước");
        _users.Should().HaveCount(4);
    }

    [Fact]
    public async Task UTCID11_ResubmitRejectedRows_NewLinkedRequest_OriginalRowsClosed()
    {
        var request = AddSubmittedRequest(1, AddGuardian(10, "a@example.com"));
        await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { RejectAll = true, RejectReason = "Sai email" });
        var row = request.Items.Single();

        var result = await _service.ResubmitRowsAsync(OfficeId, 1, new AccountRequestRowsDto { ItemIds = { row.Id } });

        result.Code.Should().Be(SystemMessages.MSG60.Code);
        var follow = _requests.Single(r => r.Id != 1);
        follow.FollowsUpRequestId.Should().Be(1);
        follow.Items.Should().ContainSingle(i => i.GuardianId == 10);
        row.Status.Should().Be(AccountRequestItemStatus.Closed);
    }

    [Fact]
    public async Task UTCID12_CloseRows_NeedsNoteAndRejectedOrFailedRows()
    {
        var request = AddSubmittedRequest(1, AddGuardian(10, "a@example.com"));
        var row = request.Items.Single();

        (await _service.CloseRowsAsync(OfficeId, 1, new AccountRequestRowsDto { ItemIds = { row.Id }, Note = "x" }))
            .Code.Should().Be(SystemMessages.MSG09.Code, "a waiting row cannot be closed");

        await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { RejectAll = true, RejectReason = "Sai" });
        (await _service.CloseRowsAsync(OfficeId, 1, new AccountRequestRowsDto { ItemIds = { row.Id } }))
            .Code.Should().Be(SystemMessages.MSG01.Code);
        (await _service.CloseRowsAsync(OfficeId, 1, new AccountRequestRowsDto { ItemIds = { row.Id }, Note = "Phụ huynh đã chuyển trường" }))
            .Success.Should().BeTrue();
        row.Status.Should().Be(AccountRequestItemStatus.Closed);
    }

    [Fact]
    public async Task UTCID13_ResendActivation_CorrectedEmail_UpdatesAccountAndRecord_StaffEmailRefused()
    {
        var g = AddGuardian(10, "sai@example.com");
        var request = AddSubmittedRequest(1, g);
        await _service.DecideAsync(AdminId, 1, new AccountRequestDecisionDto { ApproveAllValid = true });
        var row = request.Items.Single();
        var user = _users.Single(u => u.Email == "sai@example.com");
        row.CreatedUser = user;

        (await _service.ResendActivationAsync(OfficeId, row.Id, new ResendActivationDto { Email = "teacher@school.vn" }))
            .Errors.Should().ContainSingle().Which.Should().Contain("BR-68");

        var ok = await _service.ResendActivationAsync(OfficeId, row.Id, new ResendActivationDto { Email = "dung@example.com" });

        ok.Success.Should().BeTrue();
        user.Email.Should().Be("dung@example.com");
        user.Username.Should().Be("dung@example.com");
        g.Email.Should().Be("dung@example.com");
        _links.Verify(l => l.SendActivationLinkAsync(user, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UTCID14_OfficeStaff_SeeOnlyTheirOwnRequests()
    {
        AddSubmittedRequest(1, AddGuardian(10, "a@example.com"));

        (await _service.GetRequestAsync(99, false, 1)).Code.Should().Be(SystemMessages.MSG11.Code);
        (await _service.GetRequestAsync(OfficeId, false, 1)).Success.Should().BeTrue();
        (await _service.GetRequestAsync(AdminId, true, 1)).Success.Should().BeTrue();
    }
}
