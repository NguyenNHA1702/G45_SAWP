using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Services.Implementations;
using Xunit;

namespace SAWP.UnitTests;

public class LunchServiceTests : TestBase
{
    private readonly Mock<ISchoolClock> _clockMock;
    private readonly LunchService _service;

    public LunchServiceTests()
    {
        _clockMock = new Mock<ISchoolClock>();
        _clockMock.Setup(c => c.Today).Returns(new DateTime(2026, 10, 28));
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 7, 45, 0));
        _clockMock.Setup(c => c.ToSchoolTime(It.IsAny<DateTime>())).Returns((DateTime dt) => dt);

        var options = Options.Create(new SchoolOptions { LunchCutoffTime = "08:15" });
        _service = new LunchService(UnitOfWorkMock.Object, _clockMock.Object, options, AuditServiceMock.Object);
    }

    // ==================== 58. RegisterLunchAsync (2 TCs) ====================

    [Fact]
    public async Task UTCID01_RegisterLunch_Success()
    {
        // Arrange
        var targetDate = new DateTime(2026, 10, 29);
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 10, 0, 0));
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Em A" } });
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<LunchRegistration>());
        var request = new LunchRegistrationDto { StudentId = 1, Date = targetDate, MealType = MealType.StandardLunch };

        // Act
        var result = await _service.RegisterLunchAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG25.Code);
    }

    [Fact]
    public async Task UTCID02_RegisterLunch_StudentNotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<Student>());
        var request = new LunchRegistrationDto { StudentId = 999, Date = new DateTime(2026, 10, 29), MealType = MealType.StandardLunch };

        // Act
        var result = await _service.RegisterLunchAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    // ==================== 59. GetServiceDaysAsync (3 TCs) ====================

    [Fact]
    public async Task UTCID01_GetServiceDays_ValidRange_ReturnsDays()
    {
        // Arrange
        SetupRepository(new List<LunchServiceDay>());
        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 31);

        // Act
        var result = await _service.GetServiceDaysAsync(from, to);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(31);
    }

    [Fact]
    public async Task UTCID02_GetServiceDays_EndBeforeStart_Fails()
    {
        // Arrange & Act
        var result = await _service.GetServiceDaysAsync(new DateTime(2026, 10, 31), new DateTime(2026, 10, 1));

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_GetServiceDays_Exceeds92Days_Fails()
    {
        // Arrange & Act
        var result = await _service.GetServiceDaysAsync(new DateTime(2026, 1, 1), new DateTime(2026, 6, 1));

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    // ==================== 60. UpsertServiceDaysAsync (5 TCs) ====================

    [Fact]
    public async Task UTCID01_UpsertServiceDays_Success()
    {
        // Arrange
        var d1 = new DateTime(2026, 10, 29);
        var d2 = new DateTime(2026, 10, 30);
        SetupRepository(new List<LunchServiceDay> { new() { Date = d1, IsServing = true, CutoffTime = TimeSpan.FromHours(8) } });
        var request = new UpsertLunchServiceDaysRequestDto
        {
            Dates = new List<DateTime> { d1, d2 },
            IsServing = true,
            CutoffTime = "08:15",
            Remarks = "Thực đơn tuần 8"
        };

        // Act
        var result = await _service.UpsertServiceDaysAsync(15, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG44.Code);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID02_UpsertServiceDays_LongRemarks_TruncatesTo250Chars()
    {
        // Arrange
        SetupRepository(new List<LunchServiceDay>());
        var request = new UpsertLunchServiceDaysRequestDto
        {
            Dates = new List<DateTime> { new(2026, 10, 29) },
            IsServing = true,
            CutoffTime = "08:30",
            Remarks = new string('M', 300)
        };

        // Act
        var result = await _service.UpsertServiceDaysAsync(15, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data![0].Remarks!.Length.Should().Be(250);
    }

    [Fact]
    public async Task UTCID03_UpsertServiceDays_EmptyDates_Fails()
    {
        // Arrange
        var request = new UpsertLunchServiceDaysRequestDto { Dates = new List<DateTime>(), CutoffTime = "08:15" };

        // Act
        var result = await _service.UpsertServiceDaysAsync(15, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID04_UpsertServiceDays_InvalidCutoffFormat_Fails()
    {
        // Arrange
        var request = new UpsertLunchServiceDaysRequestDto
        {
            Dates = new List<DateTime> { new(2026, 10, 29) },
            CutoffTime = "invalid_time"
        };

        // Act
        var result = await _service.UpsertServiceDaysAsync(15, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID05_UpsertServiceDays_ZeroCutoffTime_Fails()
    {
        // Arrange
        var request = new UpsertLunchServiceDaysRequestDto
        {
            Dates = new List<DateTime> { new(2026, 10, 29) },
            CutoffTime = "00:00"
        };

        // Act
        var result = await _service.UpsertServiceDaysAsync(15, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    // ==================== 61. GetStudentScheduleAsync (4 TCs) ====================

    [Fact]
    public async Task UTCID01_GetStudentSchedule_LinkedParent_ReturnsSchedule()
    {
        // Arrange
        _clockMock.Setup(c => c.Today).Returns(new DateTime(2026, 10, 28));
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 8, 0, 0));
        SetupRepository(new List<Student>
        {
            new() { Id = 1, StudentCode = "HS001", FullName = "Em A", ClassAssignments = new List<ClassAssignment>() }
        });
        SetupRepository(new List<Guardian>
        {
            new() { Id = 1, UserId = 20, StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } } }
        });
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<LunchRegistration>());

        // Act
        var result = await _service.GetStudentScheduleAsync(1, "2026-10", 20, new[] { UserRoles.Parent });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.Month.Should().Be("2026-10");
        result.Data.Days.Should().HaveCount(31);
    }

    [Fact]
    public async Task UTCID02_GetStudentSchedule_StaffWithoutMonth_DefaultsToCurrentMonth()
    {
        // Arrange
        _clockMock.Setup(c => c.Today).Returns(new DateTime(2026, 10, 28));
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 8, 0, 0));
        SetupRepository(new List<Student>
        {
            new() { Id = 1, StudentCode = "HS001", FullName = "Em A", ClassAssignments = new List<ClassAssignment>() }
        });
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<LunchRegistration>());

        // Act
        var result = await _service.GetStudentScheduleAsync(1, "", 15, new[] { UserRoles.ServiceStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.Month.Should().Be("2026-10");
    }

    [Fact]
    public async Task UTCID03_GetStudentSchedule_StudentNotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<Student>());

        // Act
        var result = await _service.GetStudentScheduleAsync(999, "2026-10", 20, new[] { UserRoles.Parent });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task UTCID04_GetStudentSchedule_UnlinkedParent_Fails()
    {
        // Arrange
        SetupRepository(new List<Student>
        {
            new() { Id = 1, StudentCode = "HS001", FullName = "Em A", ClassAssignments = new List<ClassAssignment>() }
        });
        SetupRepository(new List<Guardian>());

        // Act
        var result = await _service.GetStudentScheduleAsync(1, "2026-10", 25, new[] { UserRoles.Parent });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    // ==================== 62. RegisterLunchBatchAsync (8 TCs) ====================

    [Fact]
    public async Task UTCID01_RegisterLunchBatch_Success()
    {
        // Arrange
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 10, 0, 0));
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Em A" } });
        SetupRepository(new List<Guardian>
        {
            new() { Id = 1, UserId = 20, StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } } }
        });
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<LunchRegistration>());
        SetupRepository(new List<Notification>());
        var request = new BatchLunchRegistrationRequestDto
        {
            StudentId = 1,
            Dates = new List<DateTime> { new(2026, 10, 29), new(2026, 10, 30) },
            MealType = MealType.StandardLunch
        };

        // Act
        var result = await _service.RegisterLunchBatchAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG25.Code);
        result.Data!.RegisteredCount.Should().Be(2);
    }

    [Fact]
    public async Task UTCID02_RegisterLunchBatch_ReactivatesCancelledAndSkipsActive()
    {
        // Arrange
        var d1 = new DateTime(2026, 10, 29);
        var d2 = new DateTime(2026, 10, 30);
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 10, 0, 0));
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Em A" } });
        SetupRepository(new List<Guardian>
        {
            new() { Id = 1, UserId = 20, StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } } }
        });
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<LunchRegistration>
        {
            new() { Id = 1, StudentId = 1, Date = d1, IsActive = false },
            new() { Id = 2, StudentId = 1, Date = d2, IsActive = true }
        });
        SetupRepository(new List<Notification>());
        var request = new BatchLunchRegistrationRequestDto { StudentId = 1, Dates = new List<DateTime> { d1, d2 } };

        // Act
        var result = await _service.RegisterLunchBatchAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.RegisteredCount.Should().Be(1);
        result.Data.SkippedDuplicateDates.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID03_RegisterLunchBatch_EmptyDates_Fails()
    {
        // Arrange
        var request = new BatchLunchRegistrationRequestDto { StudentId = 0, Dates = new List<DateTime>() };

        // Act
        var result = await _service.RegisterLunchBatchAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID04_RegisterLunchBatch_StudentNotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<Student>());
        var request = new BatchLunchRegistrationRequestDto { StudentId = 999, Dates = new List<DateTime> { new(2026, 10, 29) } };

        // Act
        var result = await _service.RegisterLunchBatchAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task UTCID05_RegisterLunchBatch_UnlinkedParent_Fails()
    {
        // Arrange
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Em A" } });
        SetupRepository(new List<Guardian>());
        var request = new BatchLunchRegistrationRequestDto { StudentId = 1, Dates = new List<DateTime> { new(2026, 10, 29) } };

        // Act
        var result = await _service.RegisterLunchBatchAsync(25, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID06_RegisterLunchBatch_NonServingWeekend_FailsWithMSG28()
    {
        // Arrange
        var sunday = new DateTime(2026, 11, 1);
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Em A" } });
        SetupRepository(new List<Guardian>
        {
            new() { Id = 1, UserId = 20, StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } } }
        });
        SetupRepository(new List<LunchServiceDay>());
        var request = new BatchLunchRegistrationRequestDto { StudentId = 1, Dates = new List<DateTime> { sunday } };

        // Act
        var result = await _service.RegisterLunchBatchAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG28.Code);
    }

    [Fact]
    public async Task UTCID07_RegisterLunchBatch_CutoffPassed_FailsWithMSG27()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(9));
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Em A" } });
        SetupRepository(new List<Guardian>
        {
            new() { Id = 1, UserId = 20, StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } } }
        });
        SetupRepository(new List<LunchServiceDay>());
        var request = new BatchLunchRegistrationRequestDto { StudentId = 1, Dates = new List<DateTime> { today } };

        // Act
        var result = await _service.RegisterLunchBatchAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG27.Code);
    }

    [Fact]
    public async Task UTCID08_RegisterLunchBatch_AllDatesAlreadyRegistered_FailsWithMSG29()
    {
        // Arrange
        var tomorrow = new DateTime(2026, 10, 29);
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 10, 0, 0));
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Em A" } });
        SetupRepository(new List<Guardian>
        {
            new() { Id = 1, UserId = 20, StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } } }
        });
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<LunchRegistration> { new() { Id = 1, StudentId = 1, Date = tomorrow, IsActive = true } });
        var request = new BatchLunchRegistrationRequestDto { StudentId = 1, Dates = new List<DateTime> { tomorrow } };

        // Act
        var result = await _service.RegisterLunchBatchAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG29.Code);
    }

    // ==================== 63. CancelRegistrationByParentAsync (5 TCs) ====================

    [Fact]
    public async Task UTCID01_CancelRegistrationByParent_BeforeCutoff_Success()
    {
        // Arrange
        var tomorrow = new DateTime(2026, 10, 29);
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 10, 0, 0));
        SetupRepository(new List<Guardian>
        {
            new() { Id = 1, UserId = 20, StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } } }
        });
        var reg = new LunchRegistration { Id = 1, StudentId = 1, Date = tomorrow, IsActive = true, Student = new Student { FullName = "Em A" } };
        SetupRepository(new List<LunchRegistration> { reg });
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<Notification>());
        var request = new CancelLunchRegistrationRequestDto { StudentId = 1, Date = tomorrow, Reason = "Gia đình đón về trưa" };

        // Act
        var result = await _service.CancelRegistrationByParentAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG26.Code);
        reg.IsActive.Should().BeFalse();
        reg.CancellationSource.Should().Be(LunchCancellationSource.Parent);
    }

    [Fact]
    public async Task UTCID02_CancelRegistrationByParent_InvalidStudentId_Fails()
    {
        // Arrange
        var request = new CancelLunchRegistrationRequestDto { StudentId = 0, Date = new DateTime(2026, 10, 29) };

        // Act
        var result = await _service.CancelRegistrationByParentAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_CancelRegistrationByParent_UnlinkedParent_Fails()
    {
        // Arrange
        SetupRepository(new List<Guardian>());
        var request = new CancelLunchRegistrationRequestDto { StudentId = 1, Date = new DateTime(2026, 10, 29) };

        // Act
        var result = await _service.CancelRegistrationByParentAsync(25, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID04_CancelRegistrationByParent_NoActiveRegistration_Fails()
    {
        // Arrange
        SetupRepository(new List<Guardian>
        {
            new() { Id = 1, UserId = 20, StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } } }
        });
        SetupRepository(new List<LunchRegistration>());
        var request = new CancelLunchRegistrationRequestDto { StudentId = 1, Date = new DateTime(2026, 10, 30) };

        // Act
        var result = await _service.CancelRegistrationByParentAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task UTCID05_CancelRegistrationByParent_AfterCutoff_FailsWithMSG27()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(9));
        SetupRepository(new List<Guardian>
        {
            new() { Id = 1, UserId = 20, StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } } }
        });
        SetupRepository(new List<LunchRegistration> { new() { Id = 1, StudentId = 1, Date = today, IsActive = true } });
        SetupRepository(new List<LunchServiceDay>());
        var request = new CancelLunchRegistrationRequestDto { StudentId = 1, Date = today };

        // Act
        var result = await _service.CancelRegistrationByParentAsync(20, new[] { UserRoles.Parent }, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG27.Code);
    }

    // ==================== 64. GetDailyMealCountAsync (2 TCs) ====================

    [Fact]
    public async Task UTCID01_GetDailyMealCount_HasRegistrations_ReturnsBreakdown()
    {
        // Arrange
        var date = new DateTime(2026, 10, 28);
        SetupRepository(new List<LunchRegistration>
        {
            new() { Date = date, MealType = MealType.StandardLunch, IsActive = true },
            new() { Date = date, MealType = MealType.VegetarianLunch, IsActive = true },
            new() { Date = date, MealType = MealType.SpecialDiet, IsActive = true }
        });

        // Act
        var result = await _service.GetDailyMealCountAsync(date);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.GrandTotal.Should().Be(3);
    }

    [Fact]
    public async Task UTCID02_GetDailyMealCount_NoRegistrations_ReturnsZeros()
    {
        // Arrange
        SetupRepository(new List<LunchRegistration>());

        // Act
        var result = await _service.GetDailyMealCountAsync(new DateTime(2026, 11, 1));

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.GrandTotal.Should().Be(0);
    }

    // ==================== 65. CancelMealForLeaveAsync (3 TCs) ====================

    [Fact]
    public async Task UTCID01_CancelMealForLeave_BeforeCutoffWithActiveReg_CancelsMeal()
    {
        // Arrange
        var date = new DateTime(2026, 10, 29);
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 10, 0, 0));
        SetupRepository(new List<LunchServiceDay>());
        var reg = new LunchRegistration { Id = 1, StudentId = 1, Date = date, IsActive = true };
        SetupRepository(new List<LunchRegistration> { reg });

        // Act
        var result = await _service.CancelMealForLeaveAsync(1, date, "Đơn nghỉ LR-001");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        reg.IsActive.Should().BeFalse();
        reg.CancellationSource.Should().Be(LunchCancellationSource.Leave);
    }

    [Fact]
    public async Task UTCID02_CancelMealForLeave_AfterCutoff_ReturnsFalseAndKeepsMeal()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(9));
        SetupRepository(new List<LunchServiceDay>());

        // Act
        var result = await _service.CancelMealForLeaveAsync(1, today, "Đơn nghỉ LR-001");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID03_CancelMealForLeave_NoActiveRegistration_ReturnsFalse()
    {
        // Arrange
        var date = new DateTime(2026, 10, 29);
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 10, 0, 0));
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<LunchRegistration>());

        // Act
        var result = await _service.CancelMealForLeaveAsync(1, date, "Đơn nghỉ LR-001");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    // ==================== 66. RestoreMealForStudentAsync (2 TCs) ====================

    [Fact]
    public async Task UTCID01_RestoreMealForStudent_HasCancelledReg_RestoresSuccessfully()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(7).AddMinutes(45));
        var reg = new LunchRegistration { Id = 1, StudentId = 1, Date = today, IsActive = false, CancellationSource = LunchCancellationSource.Leave };
        SetupRepository(new List<LunchRegistration> { reg });

        // Act
        var result = await _service.RestoreMealForStudentAsync(1, today, "Học sinh đi học lại buổi sáng");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        reg.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID02_RestoreMealForStudent_NoCancelledReg_ReturnsFalse()
    {
        // Arrange
        SetupRepository(new List<LunchRegistration>());

        // Act
        var result = await _service.RestoreMealForStudentAsync(2, new DateTime(2026, 10, 28), "Khôi phục suất ăn");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    // ==================== 67. GetDailyMealBoardAsync (3 TCs) ====================

    [Fact]
    public async Task UTCID01_GetDailyMealBoard_ServiceStaff_ReturnsFullBoardAndAnomalies()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        SetupRepository(new List<LunchServiceDay>());
        var cls = new Class { Id = 1, ClassName = "1A", Grade = 1, Status = "Active" };
        SetupRepository(new List<Class> { cls });
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Em A",
            ClassAssignments = new List<ClassAssignment> { new() { ClassId = 1, IsActive = true, Class = cls } }
        };
        SetupRepository(new List<LunchRegistration>
        {
            new() { Id = 10, StudentId = 1, Date = today, IsActive = false, CancellationSource = LunchCancellationSource.Leave, Student = student }
        });
        SetupRepository(new List<AttendanceRecord>
        {
            new() { StudentId = 1, Date = today, Status = AttendanceStatus.Present }
        });

        // Act
        var result = await _service.GetDailyMealBoardAsync(today, 15, new[] { UserRoles.ServiceStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.CanManage.Should().BeTrue();
        result.Data.TotalAnomalies.Should().Be(1);
    }

    [Fact]
    public async Task UTCID02_GetDailyMealBoard_TeacherRole_ScopesToHomeroomClass()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        var cls = new Class { Id = 1, ClassName = "1A", Grade = 1, HomeroomTeacherId = 10, Status = "Active" };
        SetupRepository(new List<Class> { cls });
        SetupRepository(new List<LunchRegistration>
        {
            new()
            {
                Id = 10,
                StudentId = 1,
                Date = today,
                IsActive = true,
                Student = new Student { Id = 1, ClassAssignments = new List<ClassAssignment> { new() { ClassId = 1, IsActive = true, Class = cls } } }
            }
        });
        SetupRepository(new List<AttendanceRecord>());

        // Act
        var result = await _service.GetDailyMealBoardAsync(today, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.CanManage.Should().BeFalse();
        result.Data.ClassBreakdowns.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID03_GetDailyMealBoard_FilterByClassId_ReturnsScopedClass()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        SetupRepository(new List<LunchServiceDay>());
        var cls1 = new Class { Id = 1, ClassName = "1A", Grade = 1, Status = "Active" };
        var cls2 = new Class { Id = 2, ClassName = "1B", Grade = 1, Status = "Active" };
        SetupRepository(new List<Class> { cls1, cls2 });
        SetupRepository(new List<LunchRegistration>
        {
            new()
            {
                Id = 10,
                StudentId = 1,
                Date = today,
                IsActive = true,
                Student = new Student { Id = 1, ClassAssignments = new List<ClassAssignment> { new() { ClassId = 1, IsActive = true, Class = cls1 } } }
            }
        });
        SetupRepository(new List<AttendanceRecord>());

        // Act
        var result = await _service.GetDailyMealBoardAsync(today, 15, new[] { UserRoles.ServiceStaff }, classId: 1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.ClassBreakdowns.Should().HaveCount(1);
        result.Data.ClassBreakdowns[0].ClassId.Should().Be(1);
    }

    // ==================== 68. LockDailyMealAsync (2 TCs) ====================

    [Fact]
    public async Task UTCID01_LockDailyMeal_ExistingServiceDay_UpdatesLockStatus()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        var day = new LunchServiceDay { Id = 1, Date = today, IsServing = true, CutoffTime = new TimeSpan(8, 30, 0), IsLocked = false };
        SetupRepository(new List<LunchServiceDay> { day });
        var request = new LockMealDayRequestDto { Date = today, Notes = "Đã chốt 350 suất báo bếp lúc 08:30" };

        // Act
        var result = await _service.LockDailyMealAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        day.IsLocked.Should().BeTrue();
        day.LockedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UTCID02_LockDailyMeal_NewServiceDay_CreatesLockedRecord()
    {
        // Arrange
        var tomorrow = new DateTime(2026, 10, 29);
        var days = new List<LunchServiceDay>();
        SetupRepository(days);
        var request = new LockMealDayRequestDto { Date = tomorrow, Notes = "Chốt sớm" };

        // Act
        var result = await _service.LockDailyMealAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        days.Should().HaveCount(1);
    }

    // ==================== 69. GetMealHeadcountAsync (2 TCs) ====================

    [Fact]
    public async Task UTCID01_GetMealHeadcount_ServiceDay_ReturnsHeadcountReport()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(9));
        SetupRepository(new List<LunchServiceDay>());
        LunchRepoMock.Setup(l => l.GetHeadcountRowsAsync(today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HeadcountRow>
            {
                new(1, 1, "1A", MealType.StandardLunch, true, null),
                new(2, 1, "1A", MealType.StandardLunch, false, LunchCancellationSource.Attendance)
            });
        AttendanceRepoMock.Setup(a => a.GetSubmittedClassIdsAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int> { 1 });

        // Act
        var result = await _service.GetMealHeadcountAsync(today);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.IsServiceDay.Should().BeTrue();
        result.Data.TotalMeals.Should().Be(1);
        result.Data.DeductedByAttendance.Should().Be(1);
    }

    [Fact]
    public async Task UTCID02_GetMealHeadcount_NonServiceDay_ReturnsZeroMeals()
    {
        // Arrange
        var sunday = new DateTime(2026, 11, 1);
        _clockMock.Setup(c => c.Today).Returns(new DateTime(2026, 10, 28));
        SetupRepository(new List<LunchServiceDay>());

        // Act
        var result = await _service.GetMealHeadcountAsync(sunday);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.IsServiceDay.Should().BeFalse();
        result.Data.TotalMeals.Should().Be(0);
    }
}
