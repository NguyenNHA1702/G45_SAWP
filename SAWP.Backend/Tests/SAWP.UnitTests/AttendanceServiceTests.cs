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
        result.Data.Should().HaveCount(1);
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
    public async Task UTCID05_GetAttendanceSheet_EmptyRoster_ReturnsEmptySheet()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        SetupRepository(new List<Class> { new() { Id = 2, ClassName = "1B", Status = "Active" } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(2, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student>());

        // Act
        var result = await _service.GetAttendanceSheetAsync(2, today, AttendanceSession.Morning, 10, new[] { UserRoles.OfficeStaff });

        // Assert (ATT-2: trả 200 OK với danh sách rỗng thay vì 404 MSG43)
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Entries.Should().BeEmpty();
        result.Data.CanEdit.Should().BeFalse();
        result.Data.LockReason.Should().Be("EmptyRoster");
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
    // OVR-01 (BR-62): submitting attendance never notifies parents.

    [Fact]
    public async Task UTCID01_RecordDailyAttendance_Success_DeductsLunchAndDoesNotNotifyParent()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(7).AddMinutes(45));
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<LunchServiceDay>());
        var notificationRepo = SetupRepository(new List<Notification>());
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
        notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
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
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active" } });
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
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active" } });
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
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active" } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
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
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active" } });
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

    // ==================== 55. CorrectAttendanceAsync (7 TCs) ====================

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

    [Fact]
    public async Task UTCID07_CorrectAttendance_ToUnexcusedAbsenceToday_DoesNotNotifyParent()
    {
        // Arrange: OVR-01 (BR-62) - a correction never notifies parents
        var today = new DateTime(2026, 10, 28);
        var rec = new AttendanceRecord
        {
            Id = 10,
            StudentId = 1,
            ClassId = 1,
            Date = today,
            Session = AttendanceSession.Morning,
            Status = AttendanceStatus.Present,
            Student = new Student { Id = 1, StudentCode = "HS001", FullName = "Em A" },
            Class = new Class { Id = 1, ClassName = "1A" }
        };
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rec);
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());
        var notificationRepo = SetupRepository(new List<Notification>());
        StudentRepoMock.Setup(s => s.GetGuardianUserIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<int>> { [1] = new List<int> { 20 } });

        var dto = new CorrectAttendanceDto
        {
            Status = AttendanceStatus.UnexcusedAbsence,
            CorrectionReason = "Giáo viên chọn nhầm trạng thái"
        };

        // Act
        var result = await _service.CorrectAttendanceAsync(10, dto, 10);

        // Assert
        result.Success.Should().BeTrue();
        rec.Status.Should().Be(AttendanceStatus.UnexcusedAbsence);
        rec.AbsenceNoticeSentAt.Should().BeNull();
        notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
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

    [Fact]
    public async Task UTCID07_A3_AutoLeaveAttendanceRecord_DoesNotMarkSubmittedAt()
    {
        // Arrange: lớp chỉ có bản ghi ExcusedAbsence tự sinh từ đơn xin nghỉ (LeaveRequestId != null, UpdatedByUserId == null)
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student>
            {
                new() { Id = 1, StudentCode = "HS001", FullName = "Em A" },
                new() { Id = 2, StudentCode = "HS002", FullName = "Em B" }
            });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>
            {
                new()
                {
                    Id = 100,
                    StudentId = 1,
                    ClassId = 1,
                    Date = today,
                    Session = AttendanceSession.Morning,
                    Status = AttendanceStatus.ExcusedAbsence,
                    LeaveRequestId = 5,
                    UpdatedByUserId = null,
                    RecordedByUserId = 30,
                    CreatedAt = DateTime.UtcNow
                }
            });
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>
            {
                [1] = new LeaveIndicatorDto { LeaveRequestId = 5, RequestNumber = "LR-001", Status = RequestStatus.Approved }
            });
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>
            {
                [1] = new LunchRegistration { Id = 10, StudentId = 1, Date = today, IsActive = false, CancellationSource = LunchCancellationSource.Leave }
            });

        // Act
        var result = await _service.GetAttendanceSheetAsync(1, today, AttendanceSession.Morning, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.SubmittedAt.Should().BeNull();
        result.Data.Entries[0].HasLunch.Should().BeFalse();
        result.Data.Entries[0].LunchCancelledBy.Should().Be(LunchCancellationSource.Leave);
    }

    [Fact]
    public async Task UTCID08_A7_OfficeStaffPastDay_UnmarkedAllowsBulk_MarkedLocksWithPastDay()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        var pastDate = new DateTime(2026, 10, 26);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, pastDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), pastDate, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), pastDate, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        // 1. Chưa có bản ghi do GV nộp -> Giáo vụ bổ sung được
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, pastDate, AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        var unmarkedRes = await _service.GetAttendanceSheetAsync(1, pastDate, AttendanceSession.Morning, 10, new[] { UserRoles.OfficeStaff });

        // 2. Đã có bản ghi do GV nộp -> khóa sửa hàng loạt ("PastDay")
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, pastDate, AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>
            {
                new() { Id = 200, StudentId = 1, ClassId = 1, Date = pastDate, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present, LeaveRequestId = null }
            });
        var markedRes = await _service.GetAttendanceSheetAsync(1, pastDate, AttendanceSession.Morning, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        unmarkedRes.Data!.CanEdit.Should().BeTrue();
        markedRes.Data!.CanEdit.Should().BeFalse();
        markedRes.Data.LockReason.Should().Be("PastDay");
    }

    [Fact]
    public async Task UTCID09_MakeupSaturdayAndHolidayOverride_RespectedByGetAttendanceSheet()
    {
        // Arrange: 12/12/2026 (Thứ 7 học bù = true), 02/12/2026 (Thứ 4 nghỉ = false)
        var makeupSat = new DateTime(2026, 12, 12);
        var holidayWed = new DateTime(2026, 12, 2);
        _clockMock.Setup(c => c.Today).Returns(makeupSat);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>
        {
            new() { Id = 1, Date = makeupSat, IsSchoolDay = true, Note = "Học bù 02/12" },
            new() { Id = 2, Date = holidayWed, IsSchoolDay = false, Note = "Ngày nghỉ trường" }
        });
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, It.IsAny<DateTime>(), AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        // Act
        var satSheet = await _service.GetAttendanceSheetAsync(1, makeupSat, AttendanceSession.Morning, 30, new[] { UserRoles.Teacher });
        var wedSheet = await _service.GetAttendanceSheetAsync(1, holidayWed, AttendanceSession.Morning, 30, new[] { UserRoles.Teacher });

        // Assert
        satSheet.Data!.IsSchoolDay.Should().BeTrue();
        satSheet.Data.CanEdit.Should().BeTrue();
        wedSheet.Data!.IsSchoolDay.Should().BeFalse();
        wedSheet.Data.CanEdit.Should().BeFalse();
        wedSheet.Data.LockReason.Should().Be("NonSchoolDay");
    }

    [Fact]
    public async Task UTCID10_CorrectAttendance_PastDayToExcusedAbsence_Blocked_WhileOtherStatusAllowed()
    {
        // Arrange
        var today = new DateTime(2026, 11, 24);
        var yesterday = today.AddDays(-1);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(9));

        var pastRec = new AttendanceRecord
        {
            Id = 500,
            StudentId = 1,
            ClassId = 1,
            Date = yesterday,
            Session = AttendanceSession.Morning,
            Status = AttendanceStatus.UnexcusedAbsence,
            Student = new Student { Id = 1, StudentCode = "HS001", FullName = "Nguyễn Văn A" },
            Class = new Class { Id = 1, ClassName = "1A" }
        };

        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pastRec);
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), yesterday, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());
        SetupRepository(new List<LunchServiceDay>());

        // Act
        var blockedExcused = await _service.CorrectAttendanceAsync(500, new CorrectAttendanceDto
        {
            Status = AttendanceStatus.ExcusedAbsence,
            CorrectionReason = "Muốn đổi sang có phép ngày hôm qua"
        }, 10);

        var allowedPresent = await _service.CorrectAttendanceAsync(500, new CorrectAttendanceDto
        {
            Status = AttendanceStatus.Present,
            CorrectionReason = "Giáo viên chấm nhầm dòng học sinh"
        }, 10);

        // Assert
        blockedExcused.Success.Should().BeFalse();
        blockedExcused.Code.Should().Be(SystemMessages.MSG01.Code);
        allowedPresent.Success.Should().BeTrue();
        pastRec.Status.Should().Be(AttendanceStatus.Present);
    }

    // ==================== 58. SendAbsenceNoticeAsync (6 TCs) - OVR-01, UC-23 A2 ====================

    private AttendanceRecord UnexcusedRecord(int id = 10) => new()
    {
        Id = id,
        StudentId = 1,
        ClassId = 1,
        Date = new DateTime(2026, 10, 28),
        Session = AttendanceSession.Morning,
        Status = AttendanceStatus.UnexcusedAbsence,
        Student = new Student { Id = 1, StudentCode = "HS001", FullName = "Em A" },
        Class = new Class { Id = 1, ClassName = "1A" }
    };

    private void SetupNoLeaveAndGuardians(params int[] guardianUserIds)
    {
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), It.IsAny<AttendanceSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        StudentRepoMock.Setup(s => s.GetGuardianUserIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<int>> { [1] = new List<int>(guardianUserIds) });
    }

    [Fact]
    public async Task UTCID01_SendAbsenceNotice_UnexcusedAbsence_NotifiesEveryLinkedParent()
    {
        // Arrange
        var rec = UnexcusedRecord();
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupNoLeaveAndGuardians(20, 21);
        var notificationRepo = SetupRepository(new List<Notification>());

        // Act
        var result = await _service.SendAbsenceNoticeAsync(10, 40);

        // Assert
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG55.Code);
        result.Data!.ParentsNotifiedCount.Should().Be(2);
        rec.AbsenceNoticeSentAt.Should().NotBeNull();
        rec.AbsenceNoticeSentByUserId.Should().Be(40);
        notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.ReferenceKey == NotificationTemplates.AbsenceNoticeKey(10)), It.IsAny<CancellationToken>()), Times.Exactly(2));
        AuditServiceMock.Verify(a => a.LogActionAsync(40, AuditActions.AttendanceAbsenceNoticeSend, nameof(AttendanceRecord), "10",
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID02_SendAbsenceNotice_AlreadySent_FailsWithMSG09()
    {
        // Arrange
        var rec = UnexcusedRecord();
        rec.AbsenceNoticeSentAt = new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc);
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        var notificationRepo = SetupRepository(new List<Notification>());

        // Act
        var result = await _service.SendAbsenceNoticeAsync(10, 40);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
        notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(AttendanceStatus.Present)]
    [InlineData(AttendanceStatus.Late)]
    [InlineData(AttendanceStatus.ExcusedAbsence)]
    public async Task UTCID03_SendAbsenceNotice_NotUnexcusedAbsence_FailsWithMSG09(AttendanceStatus status)
    {
        // Arrange
        var rec = UnexcusedRecord();
        rec.Status = status;
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);

        // Act
        var result = await _service.SendAbsenceNoticeAsync(10, 40);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
        rec.AbsenceNoticeSentAt.Should().BeNull();
    }

    [Fact]
    public async Task UTCID04_SendAbsenceNotice_CoveredByApprovedLeave_FailsWithMSG09()
    {
        // Arrange
        var rec = UnexcusedRecord();
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), rec.Date, rec.Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>
            {
                [1] = new LeaveIndicatorDto { LeaveRequestId = 5, Status = RequestStatus.Approved }
            });

        // Act
        var result = await _service.SendAbsenceNoticeAsync(10, 40);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
        rec.AbsenceNoticeSentAt.Should().BeNull();
    }

    [Fact]
    public async Task UTCID05_SendAbsenceNotice_NoLinkedParent_FailsWithMSG09()
    {
        // Arrange
        var rec = UnexcusedRecord();
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupNoLeaveAndGuardians();

        // Act
        var result = await _service.SendAbsenceNoticeAsync(10, 40);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
        rec.AbsenceNoticeSentAt.Should().BeNull();
    }

    [Fact]
    public async Task UTCID06_SendAbsenceNotice_RecordNotFound_FailsWithMSG43()
    {
        // Arrange
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((AttendanceRecord?)null);

        // Act
        var result = await _service.SendAbsenceNoticeAsync(999, 40);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    [Fact]
    public async Task UTCID07_SendAbsenceNotice_HomeroomTeacher_NotifiesParents()
    {
        // Arrange
        var rec = UnexcusedRecord();
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupNoLeaveAndGuardians(20);
        var notificationRepo = SetupRepository(new List<Notification>());

        // Act
        var result = await _service.SendAbsenceNoticeAsync(10, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG55.Code);
        rec.AbsenceNoticeSentByUserId.Should().Be(30);
        notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID08_SendAbsenceNotice_NonHomeroomTeacher_FailsWithMSG10()
    {
        // Arrange
        var rec = UnexcusedRecord();
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 99, Status = "Active" } });

        // Act
        var result = await _service.SendAbsenceNoticeAsync(10, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
        rec.AbsenceNoticeSentAt.Should().BeNull();
    }

    // ==================== Round 2 Regression Tests (ATT-3..ATT-13) ====================

    [Fact]
    public async Task ATT3_RecordDailyAttendance_UnchangedOrphanedExcusedAbsence_AllowsSavingOtherStudents()
    {
        // Arrange: HS1 vốn đang ExcusedAbsence nhưng không còn đơn Approved phủ; GV chỉ sửa HS2 sang Late
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(8));
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1B", Status = "Active", SchoolYearId = 1, HomeroomTeacherId = 10 } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 20 } });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student>
            {
                new() { Id = 1, StudentCode = "HS038", FullName = "HS A" },
                new() { Id = 2, StudentCode = "HS039", FullName = "HS B" }
            });
        var originalUpdatedAt = new DateTime(2026, 10, 28, 7, 30, 0, DateTimeKind.Utc);
        var existingRec1 = new AttendanceRecord { Id = 101, StudentId = 1, ClassId = 1, Date = today, Session = AttendanceSession.Morning, Status = AttendanceStatus.ExcusedAbsence, RecordedByUserId = 10, UpdatedByUserId = 10, UpdatedAt = originalUpdatedAt };
        var existingRec2 = new AttendanceRecord { Id = 102, StudentId = 2, ClassId = 1, Date = today, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present, RecordedByUserId = 10, UpdatedByUserId = 10, UpdatedAt = originalUpdatedAt };
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord> { existingRec1, existingRec2 });
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto>
            {
                new() { StudentId = 1, Status = AttendanceStatus.ExcusedAbsence },
                new() { StudentId = 2, Status = AttendanceStatus.Late }
            }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 20, new[] { UserRoles.Teacher });

        // Assert
        result.Success.Should().BeTrue();
        existingRec2.Status.Should().Be(AttendanceStatus.Late);
        existingRec2.UpdatedByUserId.Should().Be(20);
        // ATT-5: Bản ghi HS1 không đổi -> giữ nguyên UpdatedAt & UpdatedByUserId
        existingRec1.UpdatedAt.Should().Be(originalUpdatedAt);
        existingRec1.UpdatedByUserId.Should().Be(10);
    }

    [Fact]
    public async Task ATT7_GetAttendanceSheet_IgnoresCalendarDayFromOtherSchoolYear()
    {
        // Arrange: Lớp thuộc năm học 1; năm học thử nghiệm (id 99) đánh dấu ngày học là nghỉ (IsSchoolDay = false)
        var today = new DateTime(2026, 10, 28); // Thứ Tư
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active", SchoolYearId = 1 } });
        SetupRepository(new List<SchoolCalendarDay>
        {
            new() { Id = 50, SchoolYearId = 99, Date = today, IsSchoolDay = false, Note = "Năm thử nghiệm" }
        });
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        // Act
        var result = await _service.GetAttendanceSheetAsync(1, today, AttendanceSession.Morning, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.IsSchoolDay.Should().BeTrue();
        result.Data.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task ATT8_GetAttendanceSheet_ServiceStaff_DeniedWithMSG10()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active" } });

        // Act
        var result = await _service.GetAttendanceSheetAsync(1, today, AttendanceSession.Morning, 50, new[] { UserRoles.ServiceStaff });

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task ATT9_GetMyClasses_FiltersByActiveSchoolYear()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        SetupRepository(new List<SchoolYear>
        {
            new() { Id = 1, YearName = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 5, 31) },
            new() { Id = 2, YearName = "2025-2026", IsActive = false, StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 5, 31) }
        });
        SetupRepository(new List<Class>
        {
            new() { Id = 1, ClassName = "1A", Grade = 1, Status = "Active", SchoolYearId = 1 },
            new() { Id = 2, ClassName = "1A", Grade = 1, Status = "Active", SchoolYearId = 2 }
        });
        AttendanceRepoMock.Setup(a => a.GetSubmittedClassIdsAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int>());

        // Act
        var result = await _service.GetMyClassesAsync(10, new[] { UserRoles.OfficeStaff }, today, AttendanceSession.Morning);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].ClassId.Should().Be(1);
        result.Data[0].SchoolYearName.Should().Be("2026-2027");
    }

    [Fact]
    public async Task ATT11_RecordDailyAttendance_DefaultDate_FailsWithMSG01()
    {
        var dto = new SubmitAttendanceSheetDto { Date = default, Session = AttendanceSession.Morning };
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task ATT11_RecordDailyAttendance_NonExistentClass_FailsWithMSG43()
    {
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class>());

        var dto = new SubmitAttendanceSheetDto { Date = today, Session = AttendanceSession.Morning };
        var result = await _service.RecordDailyAttendanceAsync(999, dto, 10, new[] { UserRoles.OfficeStaff });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    [Fact]
    public async Task ATT11_GetClassSummary_EmptyRecords_FailsWithMSG43()
    {
        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(to);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active" } });
        AttendanceRepoMock.Setup(a => a.GetClassStatusCountsAsync(1, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudentAttendanceRowDto>());

        var result = await _service.GetClassSummaryAsync(1, from, to, 10, new[] { UserRoles.OfficeStaff });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    [Fact]
    public async Task ATT13_GetStudentHistory_FutureExcusedAbsence_ExcludedFromSummaryButKeptInDays()
    {
        var today = new DateTime(2026, 10, 28);
        var futureDay = new DateTime(2026, 12, 10);
        _clockMock.Setup(c => c.Today).Returns(today);
        StudentRepoMock.Setup(s => s.IsGuardianOfAsync(1, 100, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        StudentRepoMock.Setup(s => s.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 1, FullName = "HS A", ClassAssignments = new List<ClassAssignment>() });
        AttendanceRepoMock.Setup(a => a.GetStudentAttendanceHistoryAsync(1, new DateTime(2026, 10, 1), new DateTime(2026, 12, 31), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>
            {
                new() { Date = today, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present },
                new() { Date = futureDay, Session = AttendanceSession.Morning, Status = AttendanceStatus.ExcusedAbsence }
            });

        var result = await _service.GetStudentHistoryAsync(1, new DateTime(2026, 10, 1), new DateTime(2026, 12, 31), 100, new[] { UserRoles.Parent });

        result.Success.Should().BeTrue();
        result.Data!.Days.Should().HaveCount(2);
        result.Data.Summary.Total.Should().Be(1);
        result.Data.Summary.Present.Should().Be(1);
        result.Data.Summary.ExcusedAbsence.Should().Be(0);
    }

    [Fact]
    public async Task AT01_RecordDailyAttendance_TransferredStudentSameDay_UpdatesClassIdInsteadOfInsertingDuplicate()
    {
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(9));

        SetupRepository(new List<Class> { new() { Id = 2, ClassName = "1C", SchoolYearId = 1, HomeroomTeacherId = 10 } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(2, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 31, StudentCode = "HS031", FullName = "Nguyễn Văn A" } });

        var existingFromOldClass = new AttendanceRecord
        {
            Id = 99,
            StudentId = 31,
            ClassId = 1, // previously marked in class 1B
            Date = today,
            Session = AttendanceSession.Morning,
            Status = AttendanceStatus.Present,
            RecordedByUserId = 20
        };

        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(2, today, AttendanceSession.Morning, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        AttendanceRepoMock.Setup(a => a.GetDailyStudentAttendanceAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(31)),
                today,
                AttendanceSession.Morning,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord> { existingFromOldClass });
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto>
            {
                new() { StudentId = 31, Status = AttendanceStatus.UnexcusedAbsence }
            }
        };

        var result = await _service.RecordDailyAttendanceAsync(2, dto, 30, new[] { UserRoles.Teacher });

        result.Success.Should().BeTrue();
        existingFromOldClass.ClassId.Should().Be(2);
        existingFromOldClass.Status.Should().Be(AttendanceStatus.UnexcusedAbsence);
        AttendanceRepoMock.Verify(a => a.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AT04_RecordDailyAttendance_UnexcusedWhenApprovedLeaveExists_FailsWithConflict()
    {
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(8));
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>
            {
                [1] = new LeaveIndicatorDto { LeaveRequestId = 5, Status = RequestStatus.Approved }
            });

        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto> { new() { StudentId = 1, Status = AttendanceStatus.UnexcusedAbsence } }
        };

        var result = await _service.RecordDailyAttendanceAsync(1, dto, 30, new[] { UserRoles.Teacher });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("đơn xin nghỉ được duyệt"));
    }

    [Fact]
    public async Task AT08_SendAbsenceNotice_PastDate_FailsWithMSG09()
    {
        var rec = UnexcusedRecord();
        rec.Date = new DateTime(2026, 10, 27); // yesterday
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);

        var result = await _service.SendAbsenceNoticeAsync(10, 40);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
        result.Errors.Should().Contain(e => e.Contains("hôm nay"));
    }

    [Fact]
    public async Task AT08_CorrectAttendance_FromUnexcusedWithNoticeToPresent_ClearsNoticeAndNotifiesParent()
    {
        var today = new DateTime(2026, 10, 28);
        var rec = UnexcusedRecord();
        rec.Date = today.AddDays(-1);
        rec.AbsenceNoticeSentAt = DateTime.UtcNow.AddHours(-2);
        rec.AbsenceNoticeSentByUserId = 40;
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupRepository(new List<LunchServiceDay>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), rec.Date, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());
        StudentRepoMock.Setup(s => s.GetGuardianUserIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<int>> { [1] = new List<int> { 20 } });
        var notifRepo = SetupRepository(new List<Notification>());

        var result = await _service.CorrectAttendanceAsync(10, new CorrectAttendanceDto
        {
            Status = AttendanceStatus.Present,
            CorrectionReason = "Học sinh có đi học, GV chấm nhầm"
        }, 40);

        result.Success.Should().BeTrue();
        rec.AbsenceNoticeSentAt.Should().BeNull();
        rec.AbsenceNoticeSentByUserId.Should().BeNull();
        notifRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.Type == NotificationTemplates.AttendanceAbsentRetractType && n.UserId == 20), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AT11_OfficeStaffSupplementPastDay_WithoutReason_Fails()
    {
        var pastDate = new DateTime(2026, 10, 27);
        var dto = new SubmitAttendanceSheetDto
        {
            Date = pastDate,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto> { new() { StudentId = 1, Status = AttendanceStatus.Present } }
        };

        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("lý do"));
    }

    [Fact]
    public async Task GetClassMatrix_Exceeds31Days_FailsWithMSG01()
    {
        var result = await _service.GetClassMatrixAsync(1, new DateTime(2026, 10, 1), new DateTime(2026, 11, 15), 30, new[] { UserRoles.Teacher });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task GetClassMatrix_ValidRange_ReturnsStudentsAndSchoolDates()
    {
        var from = new DateTime(2026, 10, 26); // Monday
        var to = new DateTime(2026, 10, 28);   // Wednesday (Today)
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>());
        AttendanceRepoMock.Setup(a => a.GetClassMatrixAsync(1, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClassMatrixStudentDto>
            {
                new()
                {
                    StudentId = 1,
                    StudentCode = "HS001",
                    FullName = "Em A",
                    JoinedDate = new DateTime(2026, 9, 1),
                    Present = 2,
                    Cells = new List<ClassMatrixCellDto>
                    {
                        new() { Date = from, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present },
                        new() { Date = to, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present }
                    }
                }
            });

        var result = await _service.GetClassMatrixAsync(1, from, to, 30, new[] { UserRoles.Teacher });

        result.Success.Should().BeTrue();
        result.Data!.ClassName.Should().Be("1A");
        result.Data.Dates.Should().HaveCount(3);
        result.Data.Students.Should().HaveCount(1);
    }

    [Fact]
    public async Task RecordDailyAttendance_ExcusedWithoutApprovedLeave_WithExternalRemarks_Succeeds()
    {
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(7.5));
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto>
            {
                new() { StudentId = 1, Status = AttendanceStatus.ExcusedAbsence, Remarks = "Xin phép ngoài hệ thống" }
            }
        };

        var result = await _service.RecordDailyAttendanceAsync(1, dto, 30, new[] { UserRoles.Teacher });

        result.Success.Should().BeTrue();
        AttendanceRepoMock.Verify(a => a.AddAsync(It.Is<AttendanceRecord>(r =>
            r.StudentId == 1 && r.Status == AttendanceStatus.ExcusedAbsence && r.Remarks == "Xin phép ngoài hệ thống"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
