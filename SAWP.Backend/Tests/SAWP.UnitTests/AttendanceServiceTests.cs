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

public class AttendanceServiceTests : TestBase
{
    private readonly Mock<ISchoolClock> _clockMock;
    private readonly AttendanceService _service;

    public AttendanceServiceTests()
    {
        _clockMock = new Mock<ISchoolClock>();
        _clockMock.Setup(c => c.Today).Returns(new DateTime(2026, 10, 28));
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 10, 28, 7, 45, 0));

        var options = Options.Create(new SchoolOptions { LunchCutoffTime = "08:15" });
        _service = new AttendanceService(UnitOfWorkMock.Object, _clockMock.Object, AuditServiceMock.Object, options);
    }

    // ==================== 52. GetMyClassesAsync (3 TCs) ====================

    [Fact]
    public async Task UTCID01_GetMyClasses_AdminOrOfficeStaff_ReturnsAllActiveClasses()
    {
        // Arrange
        var classes = new List<Class>
        {
            new() { Id = 1, ClassName = "1A", Grade = 1, Status = "Active" },
            new() { Id = 2, ClassName = "2A", Grade = 2, Status = "Active" }
        };
        SetupRepository(classes);
        AttendanceRepoMock.Setup(r => r.GetSubmittedClassIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int> { 1 });

        // Act
        var result = await _service.GetMyClassesAsync(10, new[] { UserRoles.OfficeStaff }, new DateTime(2026, 10, 28), AttendanceSession.Morning);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data![0].AccessReason.Should().Be("SchoolAdmin");
    }

    [Fact]
    public async Task UTCID02_GetMyClasses_Teacher_ReturnsHomeroomAndFirstPeriod()
    {
        // Arrange
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class>
        {
            new() { Id = 1, ClassName = "1A", Grade = 1, HomeroomTeacherId = 10, Status = "Active" },
            new() { Id = 2, ClassName = "1B", Grade = 1, HomeroomTeacherId = 15, Status = "Active" }
        });
        ScheduleRepoMock.Setup(s => s.GetClassesWhereTeacherHasFirstPeriodAsync(10, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 2 });
        AttendanceRepoMock.Setup(r => r.GetSubmittedClassIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int>());

        // Act
        var result = await _service.GetMyClassesAsync(30, new[] { UserRoles.Teacher }, new DateTime(2026, 10, 28), AttendanceSession.Morning);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID03_GetMyClasses_TeacherWithoutStaffProfile_ReturnsEmpty()
    {
        // Arrange
        SetupRepository(new List<StaffProfile>());

        // Act
        var result = await _service.GetMyClassesAsync(99, new[] { UserRoles.Teacher }, new DateTime(2026, 10, 28), AttendanceSession.Morning);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    // ==================== 53. GetAttendanceSheetAsync (6 TCs) ====================

    [Fact]
    public async Task UTCID01_GetAttendanceSheet_HomeroomTeacherToday_CanEditTrue()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        // Act
        var result = await _service.GetAttendanceSheetAsync(1, today, AttendanceSession.Morning, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.CanEdit.Should().BeTrue();
        result.Data.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID02_GetAttendanceSheet_WithApprovedLeave_SuggestsExcusedAbsence()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>
            {
                [1] = new LeaveIndicatorDto { LeaveRequestId = 5, RequestNumber = "LR-001", Status = RequestStatus.Approved }
            });
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        // Act
        var result = await _service.GetAttendanceSheetAsync(1, today, AttendanceSession.Morning, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.Entries[0].SuggestedStatus.Should().Be(AttendanceStatus.ExcusedAbsence);
    }

    [Fact]
    public async Task UTCID03_GetAttendanceSheet_ClassNotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<Class>());

        // Act
        var result = await _service.GetAttendanceSheetAsync(999, new DateTime(2026, 10, 28), AttendanceSession.Morning, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    [Fact]
    public async Task UTCID04_GetAttendanceSheet_UnauthorizedTeacher_Fails()
    {
        // Arrange
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 99, UserId = 45 } });
        SetupRepository(new List<ClassSchedule>());

        // Act
        var result = await _service.GetAttendanceSheetAsync(1, new DateTime(2026, 10, 28), AttendanceSession.Morning, 45, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID05_GetAttendanceSheet_EmptyRoster_Fails()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        SetupRepository(new List<Class> { new() { Id = 2, ClassName = "1B", Status = "Active" } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(2, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student>());

        // Act
        var result = await _service.GetAttendanceSheetAsync(2, today, AttendanceSession.Morning, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    [Fact]
    public async Task UTCID06_GetAttendanceSheet_PastDateForTeacher_LocksEditing()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        var pastDate = today.AddDays(-2);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, pastDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, pastDate, AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), pastDate, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), pastDate, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        // Act
        var result = await _service.GetAttendanceSheetAsync(1, pastDate, AttendanceSession.Morning, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.CanEdit.Should().BeFalse();
        result.Data.LockReason.Should().Be("PastDay");
    }

    // ==================== 54. RecordDailyAttendanceAsync (8 TCs) ====================

    [Fact]
    public async Task UTCID01_RecordDailyAttendance_Success_DeductsLunchAndNotifiesParent()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(7).AddMinutes(45));
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<Notification>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student>
            {
                new() { Id = 1, StudentCode = "HS001", FullName = "Em A" },
                new() { Id = 2, StudentCode = "HS002", FullName = "Em B" }
            });
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>
            {
                [1] = new LeaveIndicatorDto { LeaveRequestId = 10, Status = RequestStatus.Approved }
            });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>
            {
                [1] = new LunchRegistration { Id = 100, StudentId = 1, Date = today, IsActive = true }
            });
        StudentRepoMock.Setup(s => s.GetGuardianUserIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<int>> { [2] = new List<int> { 20 } });
        NotificationRepoMock.Setup(n => n.GetExistingKeysAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto>
            {
                new() { StudentId = 1, Status = AttendanceStatus.ExcusedAbsence },
                new() { StudentId = 2, Status = AttendanceStatus.UnexcusedAbsence }
            }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG18.Code);
        result.Data!.LunchDeductedCount.Should().Be(1);
        result.Data.ParentsNotifiedCount.Should().Be(1);
    }

    [Fact]
    public async Task UTCID02_RecordDailyAttendance_PresentWithCancelledLeaveLunch_RestoresMeal()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(7).AddMinutes(50));
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<LunchServiceDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>
            {
                [1] = new LunchRegistration { Id = 100, StudentId = 1, Date = today, IsActive = false, CancellationSource = LunchCancellationSource.Leave }
            });

        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto> { new() { StudentId = 1, Status = AttendanceStatus.Present } }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.LunchRestoredCount.Should().Be(1);
    }

    [Fact]
    public async Task UTCID03_RecordDailyAttendance_PastDateForTeacher_FailsWithMSG19()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        var dto = new SubmitAttendanceSheetDto { Date = today.AddDays(-1), Session = AttendanceSession.Morning };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG19.Code);
    }

    [Fact]
    public async Task UTCID04_RecordDailyAttendance_FutureDate_Fails()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        var dto = new SubmitAttendanceSheetDto { Date = today.AddDays(1), Session = AttendanceSession.Morning };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID05_RecordDailyAttendance_TeacherNotAssigned_FailsWithMSG20()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<StaffProfile>());
        var dto = new SubmitAttendanceSheetDto { Date = today, Session = AttendanceSession.Morning };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 45, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG20.Code);
    }

    [Fact]
    public async Task UTCID06_RecordDailyAttendance_EmptyOrMissingStudents_Fails()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001" } });
        var dto = new SubmitAttendanceSheetDto { Date = today, Session = AttendanceSession.Morning, Entries = new List<DailyAttendanceEntryDto>() };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID07_RecordDailyAttendance_ExcusedWithoutApprovedLeave_Fails()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto> { new() { StudentId = 1, Status = AttendanceStatus.ExcusedAbsence } }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID08_RecordDailyAttendance_RemarksExceeds500Chars_Fails()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001" } });
        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto>
            {
                new() { StudentId = 1, Status = AttendanceStatus.Present, Remarks = new string('A', 505) }
            }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    // ==================== 55. CorrectAttendanceAsync (6 TCs) ====================

    [Fact]
    public async Task UTCID01_CorrectAttendance_ToExcusedAbsence_Success()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        var rec = new AttendanceRecord
        {
            Id = 10,
            StudentId = 1,
            ClassId = 1,
            Date = today,
            Session = AttendanceSession.Morning,
            Status = AttendanceStatus.UnexcusedAbsence,
            Student = new Student { Id = 1, StudentCode = "HS001", FullName = "Em A" },
            Class = new Class { Id = 1, ClassName = "1A" }
        };
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rec);
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>
            {
                [1] = new LeaveIndicatorDto { LeaveRequestId = 5, Status = RequestStatus.Approved }
            });
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>
            {
                [1] = new LunchRegistration { Id = 50, StudentId = 1, Date = today, IsActive = true }
            });

        var dto = new CorrectAttendanceDto
        {
            Status = AttendanceStatus.ExcusedAbsence,
            CorrectionReason = "Cập nhật theo đơn phụ huynh nộp bổ sung"
        };

        // Act
        var result = await _service.CorrectAttendanceAsync(10, dto, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG18.Code);
        rec.Status.Should().Be(AttendanceStatus.ExcusedAbsence);
    }

    [Fact]
    public async Task UTCID02_CorrectAttendance_FromExcusedToPresent_RestoresLunch()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(8));
        var rec = new AttendanceRecord
        {
            Id = 11,
            StudentId = 1,
            ClassId = 1,
            Date = today,
            Session = AttendanceSession.Morning,
            Status = AttendanceStatus.ExcusedAbsence,
            Student = new Student { Id = 1, StudentCode = "HS001", FullName = "Em A" },
            Class = new Class { Id = 1, ClassName = "1A" }
        };
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rec);
        var lunch = new LunchRegistration { Id = 50, StudentId = 1, Date = today, IsActive = false, CancellationSource = LunchCancellationSource.Attendance };
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration> { [1] = lunch });

        var dto = new CorrectAttendanceDto
        {
            Status = AttendanceStatus.Present,
            CorrectionReason = "Học sinh đến lớp muộn sau khi báo nghỉ"
        };

        // Act
        var result = await _service.CorrectAttendanceAsync(11, dto, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        lunch.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID03_CorrectAttendance_RecordNotFound_Fails()
    {
        // Arrange
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);
        var dto = new CorrectAttendanceDto { Status = AttendanceStatus.Present, CorrectionReason = "Đính chính điểm danh" };

        // Act
        var result = await _service.CorrectAttendanceAsync(999, dto, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    [Fact]
    public async Task UTCID04_CorrectAttendance_ShortReason_Fails()
    {
        // Arrange
        var rec = new AttendanceRecord { Id = 10, StudentId = 1, Status = AttendanceStatus.Present };
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rec);
        var dto = new CorrectAttendanceDto { Status = AttendanceStatus.Late, CorrectionReason = "Sai" };

        // Act
        var result = await _service.CorrectAttendanceAsync(10, dto, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID05_CorrectAttendance_ExcusedWithoutApprovedLeave_Fails()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        var rec = new AttendanceRecord { Id = 10, StudentId = 1, Date = today, Session = AttendanceSession.Morning, Status = AttendanceStatus.UnexcusedAbsence };
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rec);
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        var dto = new CorrectAttendanceDto { Status = AttendanceStatus.ExcusedAbsence, CorrectionReason = "Cho nghỉ có phép" };

        // Act
        var result = await _service.CorrectAttendanceAsync(10, dto, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID06_CorrectAttendance_NoChanges_FailsWithMSG09()
    {
        // Arrange
        var rec = new AttendanceRecord { Id = 10, StudentId = 1, Status = AttendanceStatus.Present, Remarks = "Bình thường" };
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rec);
        var dto = new CorrectAttendanceDto { Status = AttendanceStatus.Present, Remarks = "Bình thường", CorrectionReason = "Giữ nguyên trạng thái" };

        // Act
        var result = await _service.CorrectAttendanceAsync(10, dto, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    // ==================== 56. GetClassSummaryAsync (5 TCs) ====================

    [Fact]
    public async Task UTCID01_GetClassSummary_Success()
    {
        // Arrange
        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 28);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<AttendanceRecord> { new() { ClassId = 1, Date = from, Status = AttendanceStatus.Present } });
        AttendanceRepoMock.Setup(a => a.GetClassStatusCountsAsync(1, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudentAttendanceRowDto>
            {
                new() { StudentId = 1, FullName = "Em A", Present = 18, Late = 2, ExcusedAbsence = 0, UnexcusedAbsence = 0 }
            });

        // Act
        var result = await _service.GetClassSummaryAsync(1, from, to, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.AttendanceRate.Should().Be(100m);
    }

    [Fact]
    public async Task UTCID02_GetClassSummary_ToDateBeforeFromDate_Fails()
    {
        // Arrange & Act
        var result = await _service.GetClassSummaryAsync(1, new DateTime(2026, 10, 28), new DateTime(2026, 10, 1), 30, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_GetClassSummary_Exceeds366Days_Fails()
    {
        // Arrange & Act
        var result = await _service.GetClassSummaryAsync(1, new DateTime(2025, 1, 1), new DateTime(2026, 10, 28), 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID04_GetClassSummary_UnauthorizedTeacher_Fails()
    {
        // Arrange
        SetupRepository(new List<StaffProfile> { new() { Id = 99, UserId = 45 } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 } });
        SetupRepository(new List<ClassSchedule>());

        // Act
        var result = await _service.GetClassSummaryAsync(1, new DateTime(2026, 10, 1), new DateTime(2026, 10, 28), 45, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID05_GetClassSummary_ClassNotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<Class>());

        // Act
        var result = await _service.GetClassSummaryAsync(999, new DateTime(2026, 10, 1), new DateTime(2026, 10, 28), 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    // ==================== 57. GetStudentHistoryAsync (6 TCs) ====================

    [Fact]
    public async Task UTCID01_GetStudentHistory_ParentLinked_RedactsInternalRemarks()
    {
        // Arrange
        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 28);
        StudentRepoMock.Setup(s => s.IsGuardianOfAsync(1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        StudentRepoMock.Setup(s => s.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 1, FullName = "Em A", ClassAssignments = new List<ClassAssignment>() });
        AttendanceRepoMock.Setup(a => a.GetStudentAttendanceHistoryAsync(1, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>
            {
                new() { Date = from, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present, Remarks = "Ghi chú nội bộ GV" }
            });

        // Act
        var result = await _service.GetStudentHistoryAsync(1, from, to, 20, new[] { UserRoles.Parent });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.Days[0].Remarks.Should().BeNull();
    }

    [Fact]
    public async Task UTCID02_GetStudentHistory_Teacher_IncludesInternalRemarks()
    {
        // Arrange
        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 28);
        StudentRepoMock.Setup(s => s.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 1, FullName = "Em A", ClassAssignments = new List<ClassAssignment>() });
        AttendanceRepoMock.Setup(a => a.GetStudentAttendanceHistoryAsync(1, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>
            {
                new() { Date = from, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present, Remarks = "Ghi chú nội bộ GV" }
            });

        // Act
        var result = await _service.GetStudentHistoryAsync(1, from, to, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.Days[0].Remarks.Should().Be("Ghi chú nội bộ GV");
    }

    [Fact]
    public async Task UTCID03_GetStudentHistory_InvalidDateRange_Fails()
    {
        // Arrange & Act
        var result = await _service.GetStudentHistoryAsync(1, new DateTime(2026, 10, 28), new DateTime(2026, 10, 1), 20, new[] { UserRoles.Parent });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID04_GetStudentHistory_ParentNotLinked_Fails()
    {
        // Arrange
        StudentRepoMock.Setup(s => s.IsGuardianOfAsync(1, 25, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _service.GetStudentHistoryAsync(1, new DateTime(2026, 10, 1), new DateTime(2026, 10, 28), 25, new[] { UserRoles.Parent });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID05_GetStudentHistory_UnauthorizedTeacher_Fails()
    {
        // Arrange
        StudentRepoMock.Setup(s => s.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 1, ClassAssignments = new List<ClassAssignment>() });

        // Act
        var result = await _service.GetStudentHistoryAsync(1, new DateTime(2026, 10, 1), new DateTime(2026, 10, 28), 45, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID06_GetStudentHistory_StudentNotFound_Fails()
    {
        // Arrange
        StudentRepoMock.Setup(s => s.GetStudentWithProfileAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        // Act
        var result = await _service.GetStudentHistoryAsync(999, new DateTime(2026, 10, 1), new DateTime(2026, 10, 28), 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG11.Code);
    }
}
