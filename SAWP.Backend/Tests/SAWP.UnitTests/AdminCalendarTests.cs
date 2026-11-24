using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using FluentAssertions;
using Moq;
using Services.Implementations;
using Services.Interfaces;

namespace SAWP.UnitTests;

public class AdminCalendarTests : TestBase
{
    private readonly Mock<IAuditService> _auditMock;
    private readonly Mock<IAccountLinkService> _accountLinksMock;
    private readonly AdminService _service;

    public AdminCalendarTests()
    {
        _auditMock = new Mock<IAuditService>();
        _accountLinksMock = new Mock<IAccountLinkService>();
        _service = new AdminService(UnitOfWorkMock.Object, _auditMock.Object, _accountLinksMock.Object);
    }

    [Fact]
    public async Task ATT6_UpsertCalendarDay_DefaultDate_FailsWithMSG01()
    {
        var dto = new UpdateCalendarDayDto { Date = default, IsSchoolDay = false };
        var result = await _service.UpsertCalendarDayAsync(1, dto);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task ATT6_UpsertCalendarDay_NoteExceeds250Chars_FailsWithMSG01()
    {
        var dto = new UpdateCalendarDayDto
        {
            Date = new DateTime(2026, 10, 28),
            IsSchoolDay = false,
            Note = new string('X', 251)
        };
        var result = await _service.UpsertCalendarDayAsync(1, dto);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task ATT6_UpsertCalendarDay_NonExistentSchoolYear_FailsWithMSG43()
    {
        SetupRepository(new List<SchoolYear>());
        var dto = new UpdateCalendarDayDto
        {
            SchoolYearId = 999,
            Date = new DateTime(2026, 10, 28),
            IsSchoolDay = false
        };

        var result = await _service.UpsertCalendarDayAsync(1, dto);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    [Fact]
    public async Task ATT6_UpsertCalendarDay_DateOutsideSchoolYear_FailsWithMSG01()
    {
        SetupRepository(new List<SchoolYear>
        {
            new()
            {
                Id = 5,
                YearName = "2095-2096",
                StartDate = new DateTime(2095, 9, 1),
                EndDate = new DateTime(2096, 5, 31),
                IsActive = false
            }
        });

        var dto = new UpdateCalendarDayDto
        {
            SchoolYearId = 5,
            Date = new DateTime(2090, 1, 3),
            IsSchoolDay = false,
            Note = "Ngoài năm học"
        };

        var result = await _service.UpsertCalendarDayAsync(1, dto);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("03-01-2090"));
    }

    [Fact]
    public async Task DE_UpsertLunchCutoff_Outside0600To1100_FailsWithMSG01()
    {
        var res = await _service.UpsertLunchCutoffAsync(1, new UpdateLunchCutoffDto
        {
            Date = new DateTime(2026, 10, 29),
            IsServing = true,
            CutoffTime = "23:59"
        });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG01.Code);
    }
}
