using System;
using System.Collections.Generic;
using System.Linq;
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
        _clockMock.Setup(c => c.ToSchoolTime(It.IsAny<DateTime>())).Returns((DateTime utc) => utc.AddHours(7));

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
    // OVR-10 (BR-62): submitting attendance never notifies parents.

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
        // Arrange: OVR-10 (BR-62) - a correction never notifies parents
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

    // ==================== 58. Contact notes (OVR-10, UC-23 A1 / A2, BR-62) [SSAWP-155] ====================

    private AttendanceRecord UnexcusedRecord(int id = 10) => new()
    {
        Id = id,
        StudentId = 1,
        ClassId = 1,
        Date = new DateTime(2026, 10, 28),
        Session = AttendanceSession.Morning,
        Status = AttendanceStatus.UnexcusedAbsence,
        Student = new Student { Id = 1, StudentCode = "HS001", FullName = "Em A" },
        Class = new Class { Id = 1, ClassName = "1A", Grade = 1, HomeroomTeacherId = 10, Status = "Active" }
    };

    /// <summary>Teacher user 30 (staff 10) is the homeroom teacher of class 1; user 31 (staff 11) is not.</summary>
    private void SetupHomeroom()
    {
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 }, new() { Id = 11, UserId = 31 } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Grade = 1, HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<User>
        {
            new() { Id = 30, FullName = "GV Chủ nhiệm" },
            new() { Id = 31, FullName = "GV Khác" },
            new() { Id = 40, FullName = "Giáo vụ" }
        });
    }

    private static CreateAbsenceContactNoteDto ValidNote() => new()
    {
        ContactedAt = DateTime.UtcNow.AddHours(-1),
        Channel = ContactChannel.Phone,
        Outcome = ContactOutcome.Reached,
        Note = "Phụ huynh báo em bị ốm."
    };

    [Fact]
    public async Task OVR10_01_AddContactNote_HomeroomTeacher_SavesNoteAuditsAndShowsMSG55()
    {
        var rec = UnexcusedRecord();
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupHomeroom();
        var notes = new List<AbsenceContactNote>();
        SetupRepository(notes);
        var notificationRepo = SetupRepository(new List<Notification>());

        var result = await _service.AddContactNoteAsync(10, ValidNote(), 30, new[] { UserRoles.Teacher });

        result.Success.Should().BeTrue();
        result.Message.Should().Be(SystemMessages.MSG55.Content);
        notes.Should().ContainSingle();
        notes[0].AttendanceRecordId.Should().Be(10);
        notes[0].CreatedByUserId.Should().Be(30);
        notes[0].Channel.Should().Be(ContactChannel.Phone);
        notes[0].Outcome.Should().Be(ContactOutcome.Reached);
        notes[0].SupersededAt.Should().BeNull();
        result.Data!.CreatedByName.Should().Be("GV Chủ nhiệm");
        result.Data.CanSupersede.Should().BeTrue();
        AuditServiceMock.Verify(a => a.LogActionAsync(30, AuditActions.AttendanceContactNoteCreate, nameof(AbsenceContactNote), It.IsAny<string>(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        // BR-62: never notifies the Parent / Guardian
        notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OVR10_02_AddContactNote_OfficeStaff_PastAbsence_Saves()
    {
        var rec = UnexcusedRecord();
        rec.Date = new DateTime(2026, 10, 20); // no deadline for the note
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupHomeroom();
        var notes = new List<AbsenceContactNote>();
        SetupRepository(notes);

        var result = await _service.AddContactNoteAsync(10, ValidNote(), 40, new[] { UserRoles.OfficeStaff });

        result.Success.Should().BeTrue();
        notes.Should().ContainSingle(n => n.CreatedByUserId == 40);
    }

    [Theory]
    [InlineData(AttendanceStatus.Present)]
    [InlineData(AttendanceStatus.Late)]
    [InlineData(AttendanceStatus.ExcusedAbsence)]
    public async Task OVR10_03_AddContactNote_NotUnexcusedAbsence_FailsWithMSG09(AttendanceStatus status)
    {
        var rec = UnexcusedRecord();
        rec.Status = status;
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupHomeroom();
        var notes = new List<AbsenceContactNote>();
        SetupRepository(notes);

        var result = await _service.AddContactNoteAsync(10, ValidNote(), 30, new[] { UserRoles.Teacher });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task OVR10_04_AddContactNote_MissingFields_FailsWithMSG01()
    {
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(UnexcusedRecord());
        SetupHomeroom();
        var notes = new List<AbsenceContactNote>();
        SetupRepository(notes);

        var result = await _service.AddContactNoteAsync(10, new CreateAbsenceContactNoteDto { Note = "   " }, 30, new[] { UserRoles.Teacher });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().HaveCount(4);
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task OVR10_05_AddContactNote_FutureContactTimeOrLongNote_FailsWithMSG01()
    {
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(UnexcusedRecord());
        SetupHomeroom();
        SetupRepository(new List<AbsenceContactNote>());
        var future = ValidNote();
        future.ContactedAt = DateTime.UtcNow.AddDays(1);
        var tooLong = ValidNote();
        tooLong.Note = new string('a', 501);

        var r1 = await _service.AddContactNoteAsync(10, future, 30, new[] { UserRoles.Teacher });
        var r2 = await _service.AddContactNoteAsync(10, tooLong, 30, new[] { UserRoles.Teacher });

        r1.Code.Should().Be(SystemMessages.MSG01.Code);
        r2.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task OVR10_06_AddContactNote_TeacherOfAnotherClass_FailsWithMSG10()
    {
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(UnexcusedRecord());
        SetupHomeroom();
        var notes = new List<AbsenceContactNote>();
        SetupRepository(notes);

        var teacher = await _service.AddContactNoteAsync(10, ValidNote(), 31, new[] { UserRoles.Teacher });
        var board = await _service.AddContactNoteAsync(10, ValidNote(), 50, new[] { UserRoles.SchoolBoard });
        var admin = await _service.AddContactNoteAsync(10, ValidNote(), 1, new[] { UserRoles.Admin });

        teacher.Code.Should().Be(SystemMessages.MSG10.Code);
        board.Code.Should().Be(SystemMessages.MSG10.Code);
        admin.Code.Should().Be(SystemMessages.MSG10.Code);
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task OVR10_07_AddContactNote_RecordNotFound_FailsWithMSG43()
    {
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((AttendanceRecord?)null);

        var result = await _service.AddContactNoteAsync(999, ValidNote(), 40, new[] { UserRoles.OfficeStaff });

        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    private AbsenceContactNote NoteBy(int authorUserId, int id = 5) => new()
    {
        Id = id,
        AttendanceRecordId = 10,
        ContactedAt = new DateTime(2026, 10, 28, 2, 0, 0, DateTimeKind.Utc),
        Channel = ContactChannel.Zalo,
        Outcome = ContactOutcome.NotReached,
        Note = "Gọi Zalo không nghe máy.",
        CreatedByUserId = authorUserId,
        CreatedAt = new DateTime(2026, 10, 28, 2, 5, 0, DateTimeKind.Utc)
    };

    /// <summary>Record 10 of class 1A, the absence the notes of <see cref="NoteBy"/> belong to.</summary>
    private void SetupNoteRecord() =>
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(UnexcusedRecord());

    [Theory]
    [InlineData(30, UserRoles.Teacher)]     // the author
    [InlineData(40, UserRoles.OfficeStaff)] // Office Staff
    public async Task OVR10_08_SupersedeContactNote_AuthorOrOfficeStaff_MarksItAndShowsMSG319(int userId, UserRoles role)
    {
        SetupHomeroom();
        SetupNoteRecord();
        var note = NoteBy(30);
        SetupRepository(new List<AbsenceContactNote> { note });

        var result = await _service.SupersedeContactNoteAsync(5, new SupersedeAbsenceContactNoteDto { Reason = "Ghi nhầm học sinh." }, userId, new[] { role });

        result.Success.Should().BeTrue();
        result.Message.Should().Be(SystemMessages.MSG319.Content);
        note.SupersededAt.Should().NotBeNull();
        note.SupersededByUserId.Should().Be(userId);
        note.SupersedeReason.Should().Be("Ghi nhầm học sinh.");
        note.Note.Should().Be("Gọi Zalo không nghe máy."); // the note itself is kept unchanged
        result.Data!.IsSuperseded.Should().BeTrue();
        result.Data.CanSupersede.Should().BeFalse();
        AuditServiceMock.Verify(a => a.LogActionAsync(userId, AuditActions.AttendanceContactNoteSupersede, nameof(AbsenceContactNote), "5",
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OVR10_09_SupersedeContactNote_AlreadySupersededOrOtherTeacher_FailsWithMSG09()
    {
        SetupHomeroom();
        SetupNoteRecord();
        var superseded = NoteBy(30, 5);
        superseded.SupersededAt = DateTime.UtcNow.AddHours(-1);
        superseded.SupersededByUserId = 30;
        superseded.SupersedeReason = "Ghi nhầm.";
        var other = NoteBy(30, 6);
        SetupRepository(new List<AbsenceContactNote> { superseded, other });

        var again = await _service.SupersedeContactNoteAsync(5, new SupersedeAbsenceContactNoteDto { Reason = "Lý do khác nữa" }, 40, new[] { UserRoles.OfficeStaff });
        var notAuthor = await _service.SupersedeContactNoteAsync(6, new SupersedeAbsenceContactNoteDto { Reason = "Lý do khác nữa" }, 31, new[] { UserRoles.Teacher });

        again.Code.Should().Be(SystemMessages.MSG09.Code);
        superseded.SupersedeReason.Should().Be("Ghi nhầm.");
        notAuthor.Code.Should().Be(SystemMessages.MSG09.Code);
        other.SupersededAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("    ab    ")]
    public async Task OVR10_10_SupersedeContactNote_ReasonTooShort_FailsWithMSG01(string? reason)
    {
        SetupHomeroom();
        SetupNoteRecord();
        var note = NoteBy(30);
        SetupRepository(new List<AbsenceContactNote> { note });

        var result = await _service.SupersedeContactNoteAsync(5, new SupersedeAbsenceContactNoteDto { Reason = reason }, 30, new[] { UserRoles.Teacher });

        result.Code.Should().Be(SystemMessages.MSG01.Code);
        note.SupersededAt.Should().BeNull();
    }

    [Fact]
    public async Task OVR10_11_SupersedeContactNote_ReasonTooLong_FailsWithMSG01()
    {
        SetupHomeroom();
        SetupNoteRecord();
        var note = NoteBy(30);
        SetupRepository(new List<AbsenceContactNote> { note });

        var result = await _service.SupersedeContactNoteAsync(5, new SupersedeAbsenceContactNoteDto { Reason = new string('x', 501) }, 30, new[] { UserRoles.Teacher });

        result.Code.Should().Be(SystemMessages.MSG01.Code);
        note.SupersededAt.Should().BeNull();
    }

    [Fact]
    public async Task OVR10_18_AddContactNote_ContactBeforeAbsenceDay_Saves()
    {
        var rec = UnexcusedRecord();
        rec.Date = DateTime.UtcNow.Date.AddDays(-1);
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupHomeroom();
        var notes = new List<AbsenceContactNote>();
        SetupRepository(notes);
        var dto = ValidNote();
        dto.ContactedAt = rec.Date.AddDays(-1).AddHours(3); // the family called the day before; recorded later

        var result = await _service.AddContactNoteAsync(10, dto, 30, new[] { UserRoles.Teacher });

        result.Success.Should().BeTrue();
        notes.Should().ContainSingle();
    }

    [Fact]
    public async Task OVR10_19_SupersedeContactNote_AuthorNoLongerHomeroomTeacher_FailsWithMSG09()
    {
        SetupHomeroom();
        SetupNoteRecord();
        var note = NoteBy(31); // written by user 31, who is not the homeroom teacher of class 1A (any more)
        SetupRepository(new List<AbsenceContactNote> { note });

        var result = await _service.SupersedeContactNoteAsync(5, new SupersedeAbsenceContactNoteDto { Reason = "Ghi nhầm học sinh." }, 31, new[] { UserRoles.Teacher });

        result.Code.Should().Be(SystemMessages.MSG09.Code);
        note.SupersededAt.Should().BeNull();
    }

    [Fact]
    public async Task OVR10_20_SupersedeContactNote_MarkedByAnotherUserAtTheSameTime_FailsWithMSG09()
    {
        SetupHomeroom();
        SetupRepository(new List<AbsenceContactNote> { NoteBy(30) });
        UnitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("0 rows affected"));

        var result = await _service.SupersedeContactNoteAsync(5, new SupersedeAbsenceContactNoteDto { Reason = "Ghi nhầm học sinh." }, 40, new[] { UserRoles.OfficeStaff });

        result.Code.Should().Be(SystemMessages.MSG09.Code);
        UnitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        AuditServiceMock.Verify(a => a.LogActionAsync(It.IsAny<int>(), AuditActions.AttendanceContactNoteSupersede, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OVR10_12_GetContactNotes_StaffOfClassSeeNotes_ParentAndAdminDoNot()
    {
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(UnexcusedRecord());
        SetupHomeroom();
        var older = NoteBy(30, 5);
        older.SupersededAt = DateTime.UtcNow.AddHours(-1);
        older.SupersededByUserId = 40;
        older.SupersededByUser = new User { Id = 40, FullName = "Giáo vụ" };
        older.SupersedeReason = "Ghi nhầm học sinh.";
        older.CreatedByUser = new User { Id = 30, FullName = "GV Chủ nhiệm" };
        var newer = NoteBy(30, 6);
        newer.CreatedAt = older.CreatedAt.AddHours(1);
        newer.CreatedByUser = older.CreatedByUser;
        SetupRepository(new List<AbsenceContactNote> { older, newer });

        var teacher = await _service.GetContactNotesAsync(10, 30, new[] { UserRoles.Teacher });
        var board = await _service.GetContactNotesAsync(10, 50, new[] { UserRoles.SchoolBoard });
        var otherTeacher = await _service.GetContactNotesAsync(10, 31, new[] { UserRoles.Teacher });
        var parent = await _service.GetContactNotesAsync(10, 20, new[] { UserRoles.Parent });
        var admin = await _service.GetContactNotesAsync(10, 1, new[] { UserRoles.Admin });

        teacher.Success.Should().BeTrue();
        teacher.Data!.Select(n => n.Id).Should().Equal(6, 5); // newest first, superseded note still shown
        teacher.Data![1].IsSuperseded.Should().BeTrue();
        teacher.Data[1].SupersededByName.Should().Be("Giáo vụ");
        teacher.Data[0].CanSupersede.Should().BeTrue();
        board.Success.Should().BeTrue();
        board.Data!.Should().OnlyContain(n => !n.CanSupersede); // School Board only views
        otherTeacher.Code.Should().Be(SystemMessages.MSG10.Code);
        parent.Code.Should().Be(SystemMessages.MSG10.Code);
        admin.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task OVR10_13_GetAbsencesWithoutContactNote_ListsOnlyAbsencesWithNoNoteInEffect()
    {
        var cls1 = new Class { Id = 1, ClassName = "1A", Grade = 1, HomeroomTeacher = new StaffProfile { Id = 10, User = new User { FullName = "GV Chủ nhiệm" } } };
        var cls2 = new Class { Id = 2, ClassName = "2A", Grade = 2 };
        AttendanceRecord Rec(int id, Class c, DateTime d, AttendanceStatus st, params AbsenceContactNote[] notes) => new()
        {
            Id = id, StudentId = id, ClassId = c.Id, Class = c, Date = d, Session = AttendanceSession.Morning, Status = st,
            Student = new Student { Id = id, StudentCode = $"HS{id:000}", FullName = $"Em {id}" },
            ContactNotes = notes.ToList()
        };
        var day = new DateTime(2026, 10, 27);
        var supersededOnly = new AbsenceContactNote { Id = 1, SupersededAt = DateTime.UtcNow };
        var inEffect = new AbsenceContactNote { Id = 2 };
        SetupRepository(new List<AttendanceRecord>
        {
            Rec(1, cls1, day, AttendanceStatus.UnexcusedAbsence),                    // no note -> listed
            Rec(2, cls1, day, AttendanceStatus.UnexcusedAbsence, supersededOnly),    // only a superseded note -> listed
            Rec(3, cls1, day, AttendanceStatus.UnexcusedAbsence, inEffect),          // note in effect -> not listed
            Rec(4, cls1, day, AttendanceStatus.Present),                             // not an absence
            Rec(5, cls2, day, AttendanceStatus.UnexcusedAbsence),                    // other class
            Rec(6, cls1, new DateTime(2026, 9, 1), AttendanceStatus.UnexcusedAbsence) // out of range
        });

        var all = await _service.GetAbsencesWithoutContactNoteAsync(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), null, new[] { UserRoles.OfficeStaff });
        var oneClass = await _service.GetAbsencesWithoutContactNoteAsync(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), 1, new[] { UserRoles.OfficeStaff });

        all.Success.Should().BeTrue();
        all.Data!.Select(a => a.RecordId).Should().Equal(1, 2, 5);
        all.Data![0].HomeroomTeacherName.Should().Be("GV Chủ nhiệm");
        all.Data[1].SupersededNoteCount.Should().Be(1);
        oneClass.Data!.Select(a => a.RecordId).Should().Equal(1, 2);
    }

    [Fact]
    public async Task OVR10_14_GetAbsencesWithoutContactNote_NotOfficeStaffOrBadRange_Fails()
    {
        SetupRepository(new List<AttendanceRecord>());

        var teacher = await _service.GetAbsencesWithoutContactNoteAsync(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), null, new[] { UserRoles.Teacher });
        var reversed = await _service.GetAbsencesWithoutContactNoteAsync(new DateTime(2026, 10, 31), new DateTime(2026, 10, 1), null, new[] { UserRoles.OfficeStaff });

        teacher.Code.Should().Be(SystemMessages.MSG10.Code);
        reversed.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task OVR10_15_GetAttendanceSheet_ShowsContactNotesToTeacherButNotToAdmin()
    {
        var today = new DateTime(2026, 10, 28);
        SetupHomeroom();
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        var rec = UnexcusedRecord();
        rec.RecordedByUserId = 30;
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord> { rec });
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());
        SetupRepository(new List<AbsenceContactNote> { NoteBy(30) });

        var teacher = await _service.GetAttendanceSheetAsync(1, today, AttendanceSession.Morning, 30, new[] { UserRoles.Teacher });
        var admin = await _service.GetAttendanceSheetAsync(1, today, AttendanceSession.Morning, 1, new[] { UserRoles.Admin });

        teacher.Data!.Entries[0].ContactNotes.Should().ContainSingle(n => n.Id == 5);
        admin.Data!.Entries[0].ContactNotes.Should().BeEmpty();
        admin.Data.CanEdit.Should().BeFalse(); // UC-22: the System Administrator does not edit attendance
    }

    [Fact]
    public async Task OVR10_16_RecordDailyAttendance_AdminPastDay_FailsWithMSG19()
    {
        var dto = new SubmitAttendanceSheetDto
        {
            Date = new DateTime(2026, 10, 27),
            Session = AttendanceSession.Morning,
            CorrectionReason = "Bổ sung điểm danh",
            Entries = new List<DailyAttendanceEntryDto> { new() { StudentId = 1, Status = AttendanceStatus.Present } }
        };

        var result = await _service.RecordDailyAttendanceAsync(1, dto, 1, new[] { UserRoles.Admin });

        result.Code.Should().Be(SystemMessages.MSG19.Code);
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
    public async Task OVR10_17_CorrectAttendance_FromUnexcusedWithContactNoteToPresent_KeepsNoteAndNotifiesNobody()
    {
        var today = new DateTime(2026, 10, 28);
        var rec = UnexcusedRecord();
        rec.Date = today.AddDays(-1);
        var note = NoteBy(30);
        rec.ContactNotes.Add(note);
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupRepository(new List<LunchServiceDay>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), rec.Date, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());
        var notifRepo = SetupRepository(new List<Notification>());
        var noteRepo = SetupRepository(new List<AbsenceContactNote> { note });

        var result = await _service.CorrectAttendanceAsync(10, new CorrectAttendanceDto
        {
            Status = AttendanceStatus.Present,
            CorrectionReason = "Học sinh có đi học, GV chấm nhầm"
        }, 40);

        result.Success.Should().BeTrue();
        rec.ContactNotes.Should().ContainSingle(); // kept as history (BR-62)
        note.SupersededAt.Should().BeNull();
        noteRepo.Verify(r => r.DeleteAsync(It.IsAny<AbsenceContactNote>(), It.IsAny<CancellationToken>()), Times.Never);
        notifRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
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

    // ==================== Additional Coverage: All 7 Attendance Methods (N/A/B) ====================

    [Fact]
    public async Task GetMyClasses_ServiceStaffWithProfileAndInactiveClass_ReturnsEmpty()
    {
        // Arrange (ATT-8: ServiceStaff không thuộc danh sách được phép xem điểm danh lớp)
        SetupRepository(new List<SchoolYear>());
        SetupRepository(new List<StaffProfile> { new() { Id = 12, UserId = 55 } });
        SetupRepository(new List<Class>
        {
            new() { Id = 1, ClassName = "1A", Grade = 1, HomeroomTeacherId = 99, Status = "Active" },
            new() { Id = 2, ClassName = "1B", Grade = 1, HomeroomTeacherId = 12, Status = "Inactive" }
        });
        AttendanceRepoMock.Setup(a => a.GetSubmittedClassIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int>());

        // Act
        var result = await _service.GetMyClassesAsync(55, new[] { UserRoles.ServiceStaff }, new DateTime(2026, 10, 28), AttendanceSession.Morning);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyClasses_NullDate_UsesClockTodayAndCountsActiveStudents()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<SchoolYear> { new() { Id = 1, YearName = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 9, 1) } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Grade = 1, SchoolYearId = 1, Status = "Active" } });
        SetupRepository(new List<ClassAssignment>
        {
            new() { Id = 1, ClassId = 1, StudentId = 1, IsActive = true, StartDate = today.AddDays(-10), Student = new Student { Id = 1, Status = "Active" } },
            new() { Id = 2, ClassId = 1, StudentId = 2, IsActive = true, StartDate = today.AddDays(-10), Student = new Student { Id = 2, Status = "Inactive" } }
        });
        AttendanceRepoMock.Setup(a => a.GetSubmittedClassIdsAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int> { 1 });

        // Act
        var result = await _service.GetMyClassesAsync(10, new[] { UserRoles.Admin }, null, AttendanceSession.Morning);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle();
        result.Data![0].StudentCount.Should().Be(1);
        result.Data[0].IsSubmitted.Should().BeTrue();
    }

    [Fact]
    public async Task GetAttendanceSheet_FutureDate_LocksWithFuture()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        var futureDate = new DateTime(2026, 10, 29);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, futureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, futureDate, AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), futureDate, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), futureDate, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        // Act
        var result = await _service.GetAttendanceSheetAsync(1, futureDate, AttendanceSession.Morning, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.CanEdit.Should().BeFalse();
        result.Data.LockReason.Should().Be("Future");
    }

    [Fact]
    public async Task GetAttendanceSheet_OfficeStaffTodayAlreadySubmitted_LocksWithAlreadySubmitted()
    {
        // Arrange (AT-11)
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile>());
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>
            {
                new() { Id = 1, StudentId = 1, ClassId = 1, Date = today, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present, RecordedByUserId = 30, CreatedAt = DateTime.UtcNow }
            });
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        // Act
        var result = await _service.GetAttendanceSheetAsync(1, today, AttendanceSession.Morning, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.CanEdit.Should().BeFalse();
        result.Data.LockReason.Should().Be("AlreadySubmitted");
    }

    [Fact]
    public async Task GetAttendanceSheet_SchoolBoard_ViewsSheetWithNotAssignedLock()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
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
        var result = await _service.GetAttendanceSheetAsync(1, today, AttendanceSession.Morning, 2, new[] { UserRoles.SchoolBoard });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.CanEdit.Should().BeFalse();
        result.Data.LockReason.Should().Be("NotAssigned");
    }

    [Fact]
    public async Task RecordDailyAttendance_OfficeStaffSupplementPastDay_WithValidReason_Succeeds()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        var pastDate = new DateTime(2026, 10, 27);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(8));
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active" } });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, pastDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, pastDate, AttendanceSession.Morning, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), pastDate, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>());
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), pastDate, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration>());

        var dto = new SubmitAttendanceSheetDto
        {
            Date = pastDate,
            Session = AttendanceSession.Morning,
            CorrectionReason = "Giáo viên nghỉ ốm, giáo vụ bổ sung điểm danh",
            Entries = new List<DailyAttendanceEntryDto> { new() { StudentId = 1, Status = AttendanceStatus.Present } }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG18.Code);
        AttendanceRepoMock.Verify(a => a.AddAsync(It.Is<AttendanceRecord>(r => r.Date == pastDate && r.Status == AttendanceStatus.Present), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordDailyAttendance_OfficeStaffSupplementPastDay_WhenAlreadySubmitted_FailsWithMSG19()
    {
        // Arrange (A7)
        var today = new DateTime(2026, 10, 28);
        var pastDate = new DateTime(2026, 10, 27);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active" } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, pastDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, pastDate, AttendanceSession.Morning, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>
            {
                new() { Id = 50, StudentId = 1, ClassId = 1, Date = pastDate, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present, LeaveRequestId = null }
            });

        var dto = new SubmitAttendanceSheetDto
        {
            Date = pastDate,
            Session = AttendanceSession.Morning,
            CorrectionReason = "Bổ sung điểm danh ngày hôm qua",
            Entries = new List<DailyAttendanceEntryDto> { new() { StudentId = 1, Status = AttendanceStatus.Late } }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG19.Code);
    }

    [Fact]
    public async Task RecordDailyAttendance_OfficeStaffTodayAlreadySubmitted_FailsWithMSG20()
    {
        // Arrange (AT-11)
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<StaffProfile>());
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });
        AttendanceRepoMock.Setup(a => a.GetDailyClassAttendanceAsync(1, today, AttendanceSession.Morning, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>
            {
                new() { Id = 50, StudentId = 1, ClassId = 1, Date = today, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present, LeaveRequestId = null }
            });

        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto> { new() { StudentId = 1, Status = AttendanceStatus.Late } }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG20.Code);
    }

    [Fact]
    public async Task RecordDailyAttendance_DuplicateStudentEntries_FailsWithMSG01()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active" } });
        SetupRepository(new List<SchoolCalendarDay>());
        StudentRepoMock.Setup(s => s.GetActiveRosterAsync(1, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student> { new() { Id = 1, StudentCode = "HS001", FullName = "Em A" } });

        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto>
            {
                new() { StudentId = 1, Status = AttendanceStatus.Present },
                new() { StudentId = 1, Status = AttendanceStatus.Late }
            }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("trùng lặp"));
    }

    [Fact]
    public async Task RecordDailyAttendance_AfterLunchCutoff_SkipsRestoreAndAddsWarning()
    {
        // Arrange: 09:00 > 08:15 cutoff
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(9));
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
        var lunch = new LunchRegistration { Id = 100, StudentId = 1, Date = today, IsActive = false, CancellationSource = LunchCancellationSource.Leave };
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration> { [1] = lunch });

        var dto = new SubmitAttendanceSheetDto
        {
            Date = today,
            Session = AttendanceSession.Morning,
            Entries = new List<DailyAttendanceEntryDto> { new() { StudentId = 1, Status = AttendanceStatus.Present } }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.LunchRestoredCount.Should().Be(0);
        result.Data.LunchRestoreSkippedCount.Should().Be(1);
        result.Data.Warnings.Should().Contain(w => w.Contains("08:15"));
        lunch.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RecordDailyAttendance_RemarksExactly500Chars_Succeeds()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(8));
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", Status = "Active" } });
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
                new() { StudentId = 1, Status = AttendanceStatus.Present, Remarks = new string('B', 500) }
            }
        };

        // Act
        var result = await _service.RecordDailyAttendanceAsync(1, dto, 10, new[] { UserRoles.OfficeStaff });

        // Assert
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG18.Code);
    }

    [Fact]
    public async Task CorrectAttendance_ReasonExceeds500Chars_FailsWithMSG01()
    {
        // Arrange
        var rec = new AttendanceRecord { Id = 10, StudentId = 1, Date = new DateTime(2026, 10, 28), Status = AttendanceStatus.Present };
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        var dto = new CorrectAttendanceDto { Status = AttendanceStatus.Late, CorrectionReason = new string('R', 501) };

        // Act
        var result = await _service.CorrectAttendanceAsync(10, dto, 10);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task CorrectAttendance_ToUnexcusedWhenApprovedLeaveExists_FailsWithMSG01()
    {
        // Arrange (AT-04)
        var today = new DateTime(2026, 10, 28);
        var rec = new AttendanceRecord { Id = 10, StudentId = 1, Date = today, Session = AttendanceSession.Morning, Status = AttendanceStatus.Present };
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        AttendanceRepoMock.Setup(a => a.GetActiveLeavesCoveringDateAsync(It.IsAny<IEnumerable<int>>(), today, AttendanceSession.Morning, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LeaveIndicatorDto>
            {
                [1] = new LeaveIndicatorDto { LeaveRequestId = 8, Status = RequestStatus.Approved }
            });
        var dto = new CorrectAttendanceDto { Status = AttendanceStatus.UnexcusedAbsence, CorrectionReason = "Chuyển nhầm sang vắng không phép" };

        // Act
        var result = await _service.CorrectAttendanceAsync(10, dto, 10);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("đơn xin nghỉ được duyệt"));
    }

    [Fact]
    public async Task CorrectAttendance_FromExcusedToPresentAfterCutoff_DoesNotRestoreLunch()
    {
        // Arrange
        var today = new DateTime(2026, 10, 28);
        _clockMock.Setup(c => c.Today).Returns(today);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(9)); // sau 08:15
        var rec = new AttendanceRecord
        {
            Id = 12,
            StudentId = 1,
            ClassId = 1,
            Date = today,
            Session = AttendanceSession.Morning,
            Status = AttendanceStatus.ExcusedAbsence,
            Student = new Student { Id = 1, StudentCode = "HS001", FullName = "Em A" },
            Class = new Class { Id = 1, ClassName = "1A" }
        };
        AttendanceRepoMock.Setup(a => a.GetByIdWithStudentClassAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(rec);
        SetupRepository(new List<LunchServiceDay>());
        var lunch = new LunchRegistration { Id = 50, StudentId = 1, Date = today, IsActive = false, CancellationSource = LunchCancellationSource.Attendance };
        LunchRepoMock.Setup(l => l.GetRegistrationsForStudentsAsync(It.IsAny<IEnumerable<int>>(), today, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, LunchRegistration> { [1] = lunch });

        // Act
        var result = await _service.CorrectAttendanceAsync(12, new CorrectAttendanceDto
        {
            Status = AttendanceStatus.Present,
            CorrectionReason = "Học sinh đến lớp muộn sau giờ chốt cơm"
        }, 10);

        // Assert
        result.Success.Should().BeTrue();
        rec.Status.Should().Be(AttendanceStatus.Present);
        lunch.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetClassSummary_Exact366Days_ComputesRoundedAttendanceRate()
    {
        // Arrange
        var to = new DateTime(2026, 10, 28);
        var from = to.AddDays(-366);
        _clockMock.Setup(c => c.Today).Returns(to);
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<AttendanceRecord>
        {
            new() { ClassId = 1, Date = from, Status = AttendanceStatus.Present },
            new() { ClassId = 1, Date = to, Status = AttendanceStatus.UnexcusedAbsence }
        });
        AttendanceRepoMock.Setup(a => a.GetClassStatusCountsAsync(1, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudentAttendanceRowDto>
            {
                new() { StudentId = 1, FullName = "Em A", Present = 2, Late = 1, ExcusedAbsence = 0, UnexcusedAbsence = 1 }
            });

        // Act
        var result = await _service.GetClassSummaryAsync(1, from, to, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.AttendanceRate.Should().Be(75m);
        result.Data.Totals.Total.Should().Be(4);
    }

    [Fact]
    public async Task GetClassSummary_ServiceStaff_DeniedWithMSG10()
    {
        // Act
        var result = await _service.GetClassSummaryAsync(1, new DateTime(2026, 10, 1), new DateTime(2026, 10, 28), 50, new[] { UserRoles.ServiceStaff });

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task GetStudentHistory_Exceeds366Days_FailsWithMSG01()
    {
        // Arrange & Act: 26/10/2025 -> 28/10/2026 = 367 ngày (> 366 ngày)
        var result = await _service.GetStudentHistoryAsync(1, new DateTime(2025, 10, 26), new DateTime(2026, 10, 28), 20, new[] { UserRoles.Parent });

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task GetStudentHistory_HomeroomTeacher_SucceedsWithRemarks()
    {
        // Arrange
        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 28);
        var cls = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 };
        var student = new Student
        {
            Id = 1,
            FullName = "Em A",
            ClassAssignments = new List<ClassAssignment> { new() { ClassId = 1, Class = cls, IsActive = true } }
        };
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { cls });
        StudentRepoMock.Setup(s => s.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(student);
        AttendanceRepoMock.Setup(a => a.GetStudentAttendanceHistoryAsync(1, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>
            {
                new() { Date = from, Session = AttendanceSession.Morning, Status = AttendanceStatus.Late, Remarks = "Đi muộn 10 phút" }
            });

        // Act
        var result = await _service.GetStudentHistoryAsync(1, from, to, 30, new[] { UserRoles.Teacher });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ClassName.Should().Be("1A");
        result.Data.Days[0].Remarks.Should().Be("Đi muộn 10 phút");
    }
}
