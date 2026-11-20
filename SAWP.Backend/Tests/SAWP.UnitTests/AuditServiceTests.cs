using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Entities;
using Core.Enums;
using FluentAssertions;
using Moq;
using Services.Implementations;
using Xunit;

namespace SAWP.UnitTests;

public class AuditServiceTests : TestBase
{
    private readonly AuditService _service;

    public AuditServiceTests()
    {
        _service = new AuditService(UnitOfWorkMock.Object);
    }

    [Fact]
    public async Task LogAction_StoresValuesAsAsciiJsonWithEnumNames()
    {
        var repo = SetupRepository(new List<AuditLog>());
        AuditLog? stored = null;
        repo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback((AuditLog log, CancellationToken _) => stored = log)
            .ReturnsAsync((AuditLog log, CancellationToken _) => log);

        await _service.LogActionAsync(
            7,
            AuditActions.LeaveRequestCancel,
            nameof(AdministrativeRequest),
            "42",
            new { Status = RequestStatus.Submitted },
            new { Notes = "Phụ huynh tự hủy đơn." },
            "203.0.113.7");

        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(7);
        stored.Action.Should().Be("LEAVE_REQUEST_CANCEL");
        stored.EntityName.Should().Be("AdministrativeRequest");
        stored.EntityId.Should().Be("42");
        stored.IpAddress.Should().Be("203.0.113.7");
        stored.OldValues.Should().Be("{\"Status\":\"Submitted\"}");
        stored.NewValues.Should().Be("{\"Notes\":\"Ph\\u1EE5 huynh t\\u1EF1 h\\u1EE7y \\u0111\\u01A1n.\"}");
    }

    [Fact]
    public void SerializeValues_IsAsciiOnlyAndDecodesToTheOriginalText()
    {
        const string original = "Lớp 2A – <Ghi chú> & \"trích dẫn\" Đồng ý 🙂";

        var json = AuditService.SerializeValues(new { Note = original })!;

        json.Should().MatchRegex("^[\\x00-\\x7F]*$");
        JsonDocument.Parse(json).RootElement.GetProperty("Note").GetString().Should().Be(original);
    }

    [Fact]
    public async Task GetAuditLogDtos_SearchFindsVietnameseTextInEscapedValues()
    {
        SetupRepository(new List<AuditLog>
        {
            new() { Id = 1, Action = AuditActions.PostCommentDelete, EntityName = nameof(PostComment), NewValues = AuditService.SerializeValues(new { Content = "Bình luận không phù hợp" }) },
            new() { Id = 2, Action = AuditActions.ClassCreate, EntityName = nameof(Class), NewValues = AuditService.SerializeValues(new { ClassName = "2A" }) }
        });

        var result = await _service.GetAuditLogDtosPagedAsync(new Core.DTOs.AuditLogFilterDto { PageIndex = 1, PageSize = 20, Search = "bình luận" });

        result.Success.Should().BeTrue();
        result.Data!.Items.Select(x => x.Id).Should().Equal(1);
    }

    [Fact]
    public void SerializeValues_NullStaysNull()
    {
        AuditService.SerializeValues(null).Should().BeNull();
    }

    [Fact]
    public void SerializeValues_RejectsPreBuiltStrings()
    {
        var act = () => AuditService.SerializeValues("Name: 2A");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AuditActions_AreUniqueUpperSnakeCase()
    {
        var values = typeof(AuditActions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        values.Should().NotBeEmpty();
        values.Should().OnlyHaveUniqueItems();
        values.Should().OnlyContain(v => Regex.IsMatch(v, "^[A-Z]+(_[A-Z]+)+$"));
    }
}
