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
using Xunit;

namespace SAWP.UnitTests;

public class ClassServiceTests : TestBase
{
    private readonly ClassService _service;

    public ClassServiceTests()
    {
        _service = new ClassService(UnitOfWorkMock.Object, AuditServiceMock.Object);
        SetupRepository(new List<Notification>());
    }

    #region Method 10: CreateSchoolYearAsync (UTCID01 - UTCID07)

    [Fact]
    public async Task UTCID01_CreateSchoolYear_Success()
    {
        // Arrange
        var existingYears = new List<SchoolYear>();
        ClassRepoMock.Setup(r => r.GetAllSchoolYearsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingYears);
        SetupRepository(existingYears);

        var request = new CreateSchoolYearDto
        {
            YearName = "2026-2027",
            StartDate = new DateTime(2026, 9, 5),
            EndDate = new DateTime(2027, 5, 31)
        };

        // Act
        var result = await _service.CreateSchoolYearAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        result.Data!.YearName.Should().Be("2026-2027");
        result.Data.IsActive.Should().BeFalse();
        result.Data.Status.Should().Be(SchoolYearStatuses.Draft);
    }

    [Fact]
    public async Task UTCID02_CreateSchoolYear_Success_IsDraft_KeepsActiveYear()
    {
        // Arrange
        var prevActive = new SchoolYear { Id = 1, YearName = "2025-2026", IsActive = true };
        var existingYears = new List<SchoolYear> { prevActive };
        ClassRepoMock.Setup(r => r.GetAllSchoolYearsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingYears);
        SetupRepository(existingYears);

        var request = new CreateSchoolYearDto
        {
            YearName = "2026-2027",
            StartDate = new DateTime(2026, 9, 5),
            EndDate = new DateTime(2027, 5, 31)
        };

        // Act
        var result = await _service.CreateSchoolYearAsync(request, 10);

        // Assert: UC-13 step 2, BR-14, BR-72 – a new year is a Draft and the active year stays active
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.IsActive.Should().BeFalse();
        result.Data.Status.Should().Be(SchoolYearStatuses.Draft);
        prevActive.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID03_CreateSchoolYear_Fail_MissingName()
    {
        // Arrange
        var request = new CreateSchoolYearDto
        {
            YearName = "",
            StartDate = new DateTime(2026, 9, 5),
            EndDate = new DateTime(2027, 5, 31)
        };

        // Act
        var result = await _service.CreateSchoolYearAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID04_CreateSchoolYear_Fail_StartDateAfterEndDate()
    {
        // Arrange
        var request = new CreateSchoolYearDto
        {
            YearName = "2026-2027",
            StartDate = new DateTime(2027, 9, 5),
            EndDate = new DateTime(2026, 5, 31)
        };

        // Act
        var result = await _service.CreateSchoolYearAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID05_CreateSchoolYear_Fail_DuplicateName()
    {
        // Arrange
        var existingYears = new List<SchoolYear>
        {
            new() { Id = 1, YearName = "2026-2027" }
        };
        ClassRepoMock.Setup(r => r.GetAllSchoolYearsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingYears);

        var request = new CreateSchoolYearDto
        {
            YearName = "2026-2027",
            StartDate = new DateTime(2026, 9, 5),
            EndDate = new DateTime(2027, 5, 31)
        };

        // Act
        var result = await _service.CreateSchoolYearAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID06_CreateSchoolYear_Fail_DurationTooShort()
    {
        // Arrange
        var request = new CreateSchoolYearDto
        {
            YearName = "2026-Short",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 10, 1) // 30 days < 60 days
        };

        // Act
        var result = await _service.CreateSchoolYearAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID07_CreateSchoolYear_Fail_DurationTooLong()
    {
        // Arrange
        var request = new CreateSchoolYearDto
        {
            YearName = "2026-Long",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2028, 1, 1) // 730 days > 450 days
        };

        // Act
        var result = await _service.CreateSchoolYearAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID08_CreateSchoolYear_Fail_InvalidFormat_NonYearString()
    {
        var request = new CreateSchoolYearDto
        {
            YearName = "abc",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2027, 5, 31)
        };

        var result = await _service.CreateSchoolYearAsync(request, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("YYYY-YYYY"));
    }

    [Fact]
    public async Task UTCID09_CreateSchoolYear_Fail_EndYearNotStartYearPlusOne()
    {
        var request = new CreateSchoolYearDto
        {
            YearName = "2026-2028",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2027, 5, 31)
        };

        var result = await _service.CreateSchoolYearAsync(request, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("Năm kết thúc"));
    }

    [Fact]
    public async Task UTCID10_CreateClass_Fail_CapacityBelow25()
    {
        var schoolYear = new SchoolYear { Id = 1, YearName = "2026-2027" };
        SetupRepository(new List<SchoolYear> { schoolYear });

        var request = new CreateClassDto { SchoolYearId = 1, ClassName = "1A", Grade = 1, Capacity = 20 };
        var result = await _service.CreateClassAsync(request, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("25 đến 40"));
    }

    [Fact]
    public async Task UTCID11_CreateClass_Fail_CapacityAbove40()
    {
        var schoolYear = new SchoolYear { Id = 1, YearName = "2026-2027" };
        SetupRepository(new List<SchoolYear> { schoolYear });

        var request = new CreateClassDto { SchoolYearId = 1, ClassName = "1A", Grade = 1, Capacity = 45 };
        var result = await _service.CreateClassAsync(request, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("25 đến 40"));
    }

    [Fact]
    public async Task UTCID12_DeleteClass_Fail_HasHomeroomTeacher()
    {
        var cls = new Class
        {
            Id = 1,
            ClassName = "1A",
            HomeroomTeacherId = 5,
            ClassAssignments = new List<ClassAssignment>()
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        var result = await _service.DeleteClassAsync(1, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
        result.Errors.Should().Contain(e => e.Contains("Giáo viên chủ nhiệm"));
    }

    [Fact]
    public async Task UTCID13_UnassignHomeroomTeacher_Success()
    {
        var cls = new Class
        {
            Id = 1,
            ClassName = "1A",
            HomeroomTeacherId = 5,
            ClassAssignments = new List<ClassAssignment>()
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);
        SetupRepository(new List<Class> { cls });
        SetupRepository(new List<StaffProfile> { new() { Id = 5, IsHomeroomTeacher = true } });

        var result = await _service.UnassignHomeroomTeacherAsync(1, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        cls.HomeroomTeacherId.Should().BeNull();
    }

    #endregion

    #region Method 11: GetSchoolYearsAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetSchoolYears_ReturnsList()
    {
        // Arrange
        var years = new List<SchoolYear>
        {
            new() { Id = 1, YearName = "2025-2026", Classes = new List<Class>() },
            new() { Id = 2, YearName = "2026-2027", Classes = new List<Class>() }
        };
        ClassRepoMock.Setup(r => r.GetAllSchoolYearsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(years);

        // Act
        var result = await _service.GetSchoolYearsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID02_GetSchoolYears_EmptyList()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetAllSchoolYearsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SchoolYear>());

        // Act
        var result = await _service.GetSchoolYearsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    #endregion

    #region Method 12: GetActiveSchoolYearAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetActiveSchoolYear_Found()
    {
        // Arrange
        var active = new SchoolYear { Id = 1, YearName = "2026-2027", IsActive = true, Classes = new List<Class>() };
        ClassRepoMock.Setup(r => r.GetActiveSchoolYearAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);

        // Act
        var result = await _service.GetActiveSchoolYearAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.YearName.Should().Be("2026-2027");
    }

    [Fact]
    public async Task UTCID02_GetActiveSchoolYear_NotFound()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetActiveSchoolYearAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SchoolYear?)null);

        // Act
        var result = await _service.GetActiveSchoolYearAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG14.Code);
    }

    #endregion

    #region Method 13: ActivateSchoolYearAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_ActivateSchoolYear_Success()
    {
        // Arrange
        var targetYear = new SchoolYear { Id = 2, YearName = "2026-2027", IsActive = false, Status = SchoolYearStatuses.Draft };
        var oldYear = new SchoolYear { Id = 1, YearName = "2025-2026", IsActive = true };
        var allYears = new List<SchoolYear> { oldYear, targetYear };

        SetupRepository(allYears);
        SetupRepository(new List<ClassAssignment>());
        ClassRepoMock.Setup(r => r.GetAllSchoolYearsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allYears);

        // Act
        var result = await _service.ActivateSchoolYearAsync(2, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        targetYear.IsActive.Should().BeTrue();
        targetYear.Status.Should().Be(SchoolYearStatuses.Active);
        oldYear.IsActive.Should().BeFalse();
        oldYear.Status.Should().Be(SchoolYearStatuses.Archived);
    }

    [Fact]
    public async Task UTCID02_ActivateSchoolYear_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<SchoolYear>());

        // Act
        var result = await _service.ActivateSchoolYearAsync(999, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 14: CreateClassAsync (UTCID01 - UTCID05)

    [Fact]
    public async Task UTCID01_CreateClass_Success()
    {
        // Arrange
        var schoolYear = new SchoolYear { Id = 1, YearName = "2026-2027" };
        SetupRepository(new List<SchoolYear> { schoolYear });

        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class>());
        ClassRepoMock.Setup(r => r.AddAsync(It.IsAny<Class>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class c, CancellationToken _) => { c.Id = 10; return c; });

        var request = new CreateClassDto
        {
            SchoolYearId = 1,
            ClassName = "1A",
            Grade = 1,
            Capacity = 35,
            RoomNumber = "101"
        };

        // Act
        var result = await _service.CreateClassAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        result.Data!.ClassName.Should().Be("1A");
    }

    [Fact]
    public async Task UTCID02_CreateClass_Fail_InvalidGrade()
    {
        // Arrange
        var request = new CreateClassDto { SchoolYearId = 1, ClassName = "6A", Grade = 6, Capacity = 35 };

        // Act
        var result = await _service.CreateClassAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_CreateClass_Fail_InvalidCapacity()
    {
        // Arrange
        var request = new CreateClassDto { SchoolYearId = 1, ClassName = "1A", Grade = 1, Capacity = 0 };

        // Act
        var result = await _service.CreateClassAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID04_CreateClass_Fail_SchoolYearNotFound()
    {
        // Arrange
        SetupRepository(new List<SchoolYear>());
        var request = new CreateClassDto { SchoolYearId = 999, ClassName = "1A", Grade = 1, Capacity = 35 };

        // Act
        var result = await _service.CreateClassAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID05_CreateClass_Fail_DuplicateClassNameInYear()
    {
        // Arrange
        var schoolYear = new SchoolYear { Id = 1, YearName = "2026-2027" };
        SetupRepository(new List<SchoolYear> { schoolYear });

        var existingClass = new Class { Id = 10, ClassName = "1A", SchoolYearId = 1 };
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class> { existingClass });

        var request = new CreateClassDto { SchoolYearId = 1, ClassName = "1A", Grade = 1, Capacity = 35 };

        // Act
        var result = await _service.CreateClassAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 15: InitializeBatchClassesAsync (UTCID01 - UTCID04)

    [Fact]
    public async Task UTCID01_InitializeBatchClasses_Success_NewSections()
    {
        // Arrange
        var schoolYear = new SchoolYear { Id = 1, YearName = "2026-2027" };
        SetupRepository(new List<SchoolYear> { schoolYear });
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class>());
        ClassRepoMock.Setup(r => r.AddAsync(It.IsAny<Class>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class c, CancellationToken _) => c);

        var request = new InitializeBatchClassesDto
        {
            SchoolYearId = 1,
            TargetGrade = 1,
            SectionNames = new List<string> { "A", "B", "C" },
            CapacityPerClass = 35
        };

        // Act
        var result = await _service.InitializeBatchClassesAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task UTCID02_InitializeBatchClasses_UpdatesExistingCapacity()
    {
        // Arrange
        var schoolYear = new SchoolYear { Id = 1, YearName = "2026-2027" };
        SetupRepository(new List<SchoolYear> { schoolYear });

        var existingClass = new Class { Id = 1, ClassName = "1A", Grade = 1, SchoolYearId = 1, Capacity = 30 };
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class> { existingClass });

        var request = new InitializeBatchClassesDto
        {
            SchoolYearId = 1,
            TargetGrade = 1,
            SectionNames = new List<string> { "A" },
            CapacityPerClass = 40
        };

        // Act
        var result = await _service.InitializeBatchClassesAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        existingClass.Capacity.Should().Be(40);
    }

    [Fact]
    public async Task UTCID03_InitializeBatchClasses_Fail_InvalidSchoolYear()
    {
        // Arrange
        SetupRepository(new List<SchoolYear>());
        ClassRepoMock.Setup(r => r.GetActiveSchoolYearAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SchoolYear?)null);

        var request = new InitializeBatchClassesDto { SchoolYearId = 999 };

        // Act
        var result = await _service.InitializeBatchClassesAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID04_InitializeBatchClasses_SavesAuditLogAfterLogging()
    {
        // Arrange: SSAWP-106 - the audit entry must be saved, not only staged
        var schoolYear = new SchoolYear { Id = 1, YearName = "2026-2027" };
        SetupRepository(new List<SchoolYear> { schoolYear });
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class>());
        ClassRepoMock.Setup(r => r.AddAsync(It.IsAny<Class>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class c, CancellationToken _) => c);

        var calls = new List<string>();
        AuditServiceMock
            .Setup(a => a.LogActionAsync(It.IsAny<int?>(), AuditActions.ClassInitializeBatch, nameof(Class), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("log"))
            .Returns(Task.CompletedTask);
        UnitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save"))
            .ReturnsAsync(1);

        var request = new InitializeBatchClassesDto
        {
            SchoolYearId = 1,
            TargetGrade = 2,
            SectionNames = new List<string> { "A", "B", "C" },
            CapacityPerClass = 35
        };

        // Act
        var result = await _service.InitializeBatchClassesAsync(request, 10);

        // Assert
        result.Success.Should().BeTrue();
        calls.Should().Contain("log");
        calls.Last().Should().Be("save");
    }

    #endregion

    #region Method 16: GetClassesAsync (UTCID01 - UTCID03)

    [Fact]
    public async Task UTCID01_GetClasses_WithFilter_ReturnsMatching()
    {
        // Arrange
        var classes = new List<Class>
        {
            new() { Id = 1, ClassName = "1A", Grade = 1, SchoolYearId = 1, SchoolYear = new SchoolYear { YearName = "2026-2027" } }
        };
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, 1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(classes);

        // Act
        var result = await _service.GetClassesAsync(1, 1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID02_GetClasses_NoFilter_ReturnsActiveYearClasses()
    {
        // Arrange
        var activeYear = new SchoolYear { Id = 1, YearName = "2026-2027", IsActive = true };
        ClassRepoMock.Setup(r => r.GetActiveSchoolYearAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeYear);
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class>
            {
                new() { Id = 1, ClassName = "1A", SchoolYear = activeYear },
                new() { Id = 2, ClassName = "2A", SchoolYear = activeYear }
            });

        // Act
        var result = await _service.GetClassesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID03_GetClasses_NoMatching_ReturnsEmpty()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, 5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class>());

        // Act
        var result = await _service.GetClassesAsync(1, 5);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    #endregion

    #region Method 17: GetClassDetailsAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetClassDetails_Found()
    {
        // Arrange
        var cls = new Class
        {
            Id = 1,
            ClassName = "1A",
            Grade = 1,
            SchoolYear = new SchoolYear { YearName = "2026-2027" },
            ClassAssignments = new List<ClassAssignment>()
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        // Act
        var result = await _service.GetClassDetailsAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.ClassName.Should().Be("1A");
    }

    [Fact]
    public async Task UTCID02_GetClassDetails_NotFound()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class?)null);

        // Act
        var result = await _service.GetClassDetailsAsync(999);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 18: UpdateClassAsync (UTCID01 - UTCID04)

    [Fact]
    public async Task UTCID01_UpdateClass_Success()
    {
        // Arrange
        var cls = new Class { Id = 1, ClassName = "1A", SchoolYearId = 1, Capacity = 35, RoomNumber = "101", SchoolYear = new SchoolYear { YearName = "2026-2027" } };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class> { cls });

        var request = new UpdateClassDto { ClassName = "1A", Grade = 1, RoomNumber = "102-Renovated", Capacity = 35, Status = "Active" };

        // Act
        var result = await _service.UpdateClassAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        cls.RoomNumber.Should().Be("102-Renovated");
    }

    [Fact]
    public async Task UTCID02_UpdateClass_NotFound_Fails()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class?)null);

        var request = new UpdateClassDto { ClassName = "Ghost", Capacity = 35 };

        // Act
        var result = await _service.UpdateClassAsync(999, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_UpdateClass_DuplicateName_Fails()
    {
        // Arrange
        var cls = new Class { Id = 1, ClassName = "1A", SchoolYearId = 1, Capacity = 35 };
        var anotherClass = new Class { Id = 2, ClassName = "1B", SchoolYearId = 1, Capacity = 35 };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class> { cls, anotherClass });

        var request = new UpdateClassDto { ClassName = "1B", Capacity = 35 }; // Conflict with class 2

        // Act
        var result = await _service.UpdateClassAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID04_UpdateClass_CapacityBelowEnrolled_Fails()
    {
        // Arrange
        var assignments = Enumerable.Range(1, 30).Select(i => new ClassAssignment { IsActive = true }).ToList();
        var cls = new Class { Id = 1, ClassName = "1A", SchoolYearId = 1, Capacity = 35, ClassAssignments = assignments };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class> { cls });

        var request = new UpdateClassDto { ClassName = "1A", Grade = 1, Capacity = 25 }; // 25 < 30 enrolled

        // Act
        var result = await _service.UpdateClassAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG15.Code);
    }

    #endregion

    #region Method 19: GetEligibleTeachersAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetEligibleTeachers_ReturnsList()
    {
        // Arrange
        var teachers = new List<StaffProfile>
        {
            new() { Id = 1, StaffCode = "GV001", User = new User { FullName = "Co Giao Mai" } }
        };
        ClassRepoMock.Setup(r => r.GetEligibleTeachersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(teachers);
        ClassRepoMock.Setup(r => r.GetActiveSchoolYearAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchoolYear { Id = 1 });
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class>());

        // Act
        var result = await _service.GetEligibleTeachersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID02_GetEligibleTeachers_EmptyList()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetEligibleTeachersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StaffProfile>());
        ClassRepoMock.Setup(r => r.GetActiveSchoolYearAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchoolYear { Id = 1 });
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class>());

        // Act
        var result = await _service.GetEligibleTeachersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    #endregion

    #region Method 20: AssignHomeroomTeacherAsync (UTCID01 - UTCID04)

    [Fact]
    public async Task UTCID01_AssignHomeroomTeacher_Success()
    {
        // Arrange
        var cls = new Class { Id = 1, ClassName = "1A", SchoolYearId = 1 };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        var staff = new StaffProfile { Id = 10, StaffCode = "GV001", UserId = 30, User = new User { FullName = "Co Mai" } };
        SetupRepository(new List<StaffProfile> { staff });
        ClassRepoMock.Setup(r => r.IsTeacherAssignedToActiveHomeroomAsync(10, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new AssignTeacherDto { StaffProfileId = 10 };

        // Act
        var result = await _service.AssignHomeroomTeacherAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        cls.HomeroomTeacherId.Should().Be(10);
    }

    [Fact]
    public async Task UTCID02_AssignHomeroomTeacher_ClassNotFound()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class?)null);

        var request = new AssignTeacherDto { StaffProfileId = 10 };

        // Act
        var result = await _service.AssignHomeroomTeacherAsync(999, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_AssignHomeroomTeacher_AlreadyAssigned_Fails()
    {
        // Arrange
        var cls = new Class { Id = 1, ClassName = "1A", SchoolYearId = 1 };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        var staff = new StaffProfile { Id = 10, StaffCode = "GV001" };
        SetupRepository(new List<StaffProfile> { staff });
        ClassRepoMock.Setup(r => r.IsTeacherAssignedToActiveHomeroomAsync(10, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new AssignTeacherDto { StaffProfileId = 10 };

        // Act
        var result = await _service.AssignHomeroomTeacherAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID04_AssignHomeroomTeacher_StaffProfileNotFound()
    {
        // Arrange
        var cls = new Class { Id = 1, ClassName = "1A", SchoolYearId = 1 };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);
        SetupRepository(new List<StaffProfile>());

        var request = new AssignTeacherDto { StaffProfileId = 999 };

        // Act
        var result = await _service.AssignHomeroomTeacherAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 21: GetEligibleStudentsForAssignmentAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetEligibleStudents_ReturnsList()
    {
        // Arrange
        var cls = new Class { Id = 1, Grade = 1, SchoolYearId = 1 };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        var students = new List<Student>
        {
            new() { Id = 1, StudentCode = "HS001", FullName = "Nguyen Van A", Status = "Active" }
        };
        ClassRepoMock.Setup(r => r.GetEligibleStudentsForAssignmentAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(students);

        // Act
        var result = await _service.GetEligibleStudentsForAssignmentAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID02_GetEligibleStudents_ClassNotFound()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class?)null);

        // Act
        var result = await _service.GetEligibleStudentsForAssignmentAsync(999);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 22: AssignStudentsToClassAsync (UTCID01 - UTCID05)

    [Fact]
    public async Task UTCID01_AssignStudents_Success()
    {
        // Arrange
        var cls = new Class
        {
            Id = 1,
            Capacity = 35,
            SchoolYearId = 1,
            SchoolYear = new SchoolYear { YearName = "2026-2027" },
            ClassAssignments = new List<ClassAssignment>()
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        SetupRepository(new List<ClassAssignment>());

        var request = new AssignStudentsDto { StudentIds = new List<int> { 1, 2 } };

        // Act
        var result = await _service.AssignStudentsToClassAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG16.Code);
    }

    [Fact]
    public async Task UTCID02_AssignStudents_ClassNotFound()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class?)null);

        var request = new AssignStudentsDto { StudentIds = new List<int> { 1 } };

        // Act
        var result = await _service.AssignStudentsToClassAsync(999, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_AssignStudents_ExceedsCapacity_Fails()
    {
        // Arrange
        var assignments = Enumerable.Range(1, 30).Select(i => new ClassAssignment { IsActive = true }).ToList();
        var cls = new Class { Id = 1, Capacity = 35, ClassAssignments = assignments };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        var request = new AssignStudentsDto { StudentIds = new List<int> { 1, 2, 3, 4, 5, 6 } }; // 30 + 6 > 35

        // Act
        var result = await _service.AssignStudentsToClassAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG15.Code);
    }

    [Fact]
    public async Task UTCID04_AssignStudents_EmptyList_Fails()
    {
        // Arrange
        var request = new AssignStudentsDto { StudentIds = new List<int>() };

        // Act
        var result = await _service.AssignStudentsToClassAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID05_AssignStudents_ExactCapacity_Success()
    {
        // Arrange
        var clsInstance = new Class { Id = 1, SchoolYearId = 1, Capacity = 35, SchoolYear = new SchoolYear { YearName = "2026-2027" } };
        var assignments = Enumerable.Range(1, 30)
            .Select(i => new ClassAssignment { IsActive = true, ClassId = 1, Class = clsInstance })
            .ToList();
        clsInstance.ClassAssignments = assignments;

        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clsInstance);

        SetupRepository(new List<Student>
        {
            new() { Id = 31 }, new() { Id = 32 }, new() { Id = 33 }, new() { Id = 34 }, new() { Id = 35 }
        });
        SetupRepository(assignments);

        var request = new AssignStudentsDto { StudentIds = new List<int> { 31, 32, 33, 34, 35 } }; // 30 + 5 == 35

        // Act
        var result = await _service.AssignStudentsToClassAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG16.Code);
    }

    #endregion

    #region Method 23: RemoveStudentFromClassAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_RemoveStudentFromClass_Success()
    {
        // Arrange
        var assignment = new ClassAssignment { Id = 1, ClassId = 1, StudentId = 10, IsActive = true };
        SetupRepository(new List<ClassAssignment> { assignment });

        // Act
        var result = await _service.RemoveStudentFromClassAsync(1, 10, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        assignment.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID02_RemoveStudentFromClass_StudentNotEnrolled_Fails()
    {
        // Arrange
        SetupRepository(new List<ClassAssignment>());

        // Act
        var result = await _service.RemoveStudentFromClassAsync(1, 999, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_RemoveStudentsBatchFromClass_Success()
    {
        // Arrange
        var a1 = new ClassAssignment { Id = 1, ClassId = 1, StudentId = 10, IsActive = true };
        var a2 = new ClassAssignment { Id = 2, ClassId = 1, StudentId = 11, IsActive = true };
        SetupRepository(new List<ClassAssignment> { a1, a2 });

        // Act
        var result = await _service.RemoveStudentsBatchFromClassAsync(1, new List<int> { 10, 11 }, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        a1.IsActive.Should().BeFalse();
        a2.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID04_RemoveStudentsBatchFromClass_EmptyList_Fails()
    {
        // Act
        var result = await _service.RemoveStudentsBatchFromClassAsync(1, new List<int>(), 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID05_RemoveStudentsBatchFromClass_NoneFound_Fails()
    {
        // Arrange
        SetupRepository(new List<ClassAssignment>());

        // Act
        var result = await _service.RemoveStudentsBatchFromClassAsync(1, new List<int> { 999 }, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 26: GetClassRosterAsync (UTCID01 - UTCID04)

    [Fact]
    public async Task UTCID01_GetClassRoster_AdminOfficeStaff_Success()
    {
        // Arrange
        var cls = new Class
        {
            Id = 1,
            ClassName = "1A",
            Grade = 1,
            SchoolYear = new SchoolYear { YearName = "2026-2027" },
            ClassAssignments = new List<ClassAssignment>()
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        // Act
        var result = await _service.GetClassRosterAsync(1, 10, "OfficeStaff");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.ClassName.Should().Be("1A");
    }

    [Fact]
    public async Task UTCID02_GetClassRoster_HomeroomTeacher_Success()
    {
        // Arrange
        var cls = new Class
        {
            Id = 1,
            ClassName = "1A",
            Grade = 1,
            SchoolYear = new SchoolYear { YearName = "2026-2027" },
            HomeroomTeacher = new StaffProfile { UserId = 30 },
            ClassAssignments = new List<ClassAssignment>()
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        // Act
        var result = await _service.GetClassRosterAsync(1, 30, "Teacher");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.ClassName.Should().Be("1A");
    }

    [Fact]
    public async Task UTCID03_GetClassRoster_OtherTeacher_Forbidden()
    {
        // Arrange
        var cls = new Class
        {
            Id = 1,
            ClassName = "1A",
            HomeroomTeacher = new StaffProfile { UserId = 30 },
            ClassAssignments = new List<ClassAssignment>()
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        // Act
        var result = await _service.GetClassRosterAsync(1, 35, "Teacher"); // 35 != 30

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG20.Code);
    }

    [Fact]
    public async Task UTCID04_GetClassRoster_ClassNotFound()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class?)null);

        // Act
        var result = await _service.GetClassRosterAsync(999, 10, "OfficeStaff");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 27: GetMyHomeroomClassAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetMyHomeroomClass_Found()
    {
        // Arrange
        var cls = new Class
        {
            Id = 1,
            ClassName = "1A",
            Grade = 1,
            SchoolYear = new SchoolYear { YearName = "2026-2027" },
            HomeroomTeacher = new StaffProfile { UserId = 30 },
            ClassAssignments = new List<ClassAssignment>()
        };
        ClassRepoMock.Setup(r => r.GetHomeroomClassByTeacherUserIdAsync(30, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cls);

        // Act
        var result = await _service.GetMyHomeroomClassAsync(30);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.ClassName.Should().Be("1A");
    }

    [Fact]
    public async Task UTCID02_GetMyHomeroomClass_NotFound()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetHomeroomClassByTeacherUserIdAsync(35, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class?)null);

        // Act
        var result = await _service.GetMyHomeroomClassAsync(35);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG43.Code);
    }

    #endregion

    #region Method 15: DeleteClassAsync

    [Fact]
    public async Task UTCID01_DeleteClass_WithEnrolledStudents_Fails()
    {
        // Arrange
        var targetClass = new Class
        {
            Id = 50,
            ClassName = "1A",
            Grade = 1,
            ClassAssignments = new List<ClassAssignment>
            {
                new() { Id = 1, ClassId = 50, StudentId = 101, IsActive = true }
            }
        };

        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetClass);

        // Act
        var result = await _service.DeleteClassAsync(50, 1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Chỉ có thể xóa lớp học khi sĩ số bằng 0"));
    }

    [Fact]
    public async Task UTCID02_DeleteClass_EmptyClass_Success()
    {
        // Arrange
        var targetClass = new Class
        {
            Id = 51,
            ClassName = "1B",
            Grade = 1,
            ClassAssignments = new List<ClassAssignment>()
        };

        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetClass);
        SetupRepository(new List<AttendanceRecord>());
        SetupRepository(new List<ClassSchedule>());
        SetupRepository(new List<ClassAssignment>());

        // Act
        var result = await _service.DeleteClassAsync(51, 1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        ClassRepoMock.Verify(r => r.DeleteAsync(targetClass, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID03_DeleteClass_NotFound_Fails()
    {
        // Arrange
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Class?)null);

        // Act
        var result = await _service.DeleteClassAsync(999, 1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Theory]
    [InlineData(1, "2A")]
    [InlineData(1, "1<b>Z")]
    [InlineData(1, "1AB")]
    [InlineData(2, "21")]
    public async Task SC13_CreateClass_InvalidNameOrGradeMismatch_FailsWithMSG01(int grade, string className)
    {
        var request = new CreateClassDto { SchoolYearId = 1, ClassName = className, Grade = grade, Capacity = 35 };

        var result = await _service.CreateClassAsync(request, 10);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 28: Draft school year and class review (UC-13, UC-14 A2, UC-16 A1, BR-72)

    private static SchoolYear DraftYear(int id = 2) => new()
    {
        Id = id,
        YearName = "2027-2028",
        StartDate = new DateTime(2027, 9, 1),
        EndDate = new DateTime(2028, 5, 31),
        IsActive = false,
        Status = SchoolYearStatuses.Draft
    };

    [Fact]
    public async Task UTCID01_ActivateSchoolYear_NotDraft_ReturnsMSG63()
    {
        // Arrange
        var archived = new SchoolYear { Id = 3, YearName = "2024-2025", IsActive = false, Status = SchoolYearStatuses.Archived };
        SetupRepository(new List<SchoolYear> { archived });

        // Act
        var result = await _service.ActivateSchoolYearAsync(3, 10);

        // Assert: UC-13 E2
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG63.Code);
        archived.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID02_ActivateSchoolYear_ClassNotReviewed_ReturnsMSG63()
    {
        // Arrange
        var draft = DraftYear();
        SetupRepository(new List<SchoolYear> { draft });
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(2, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class>
            {
                new() { Id = 1, ClassName = "1A", SchoolYearId = 2, IsReviewed = true },
                new() { Id = 2, ClassName = "1B", SchoolYearId = 2, IsReviewed = false }
            });

        // Act
        var result = await _service.ActivateSchoolYearAsync(2, 10);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG63.Code);
        result.Errors.Should().ContainSingle(e => e.Contains("1B"));
        draft.Status.Should().Be(SchoolYearStatuses.Draft);
    }

    [Fact]
    public async Task UTCID03_ActivateSchoolYear_AllReviewed_DraftAssignmentsBecomeCurrent()
    {
        // Arrange: student 1 is in 1A of the active year and placed in 2A of the Draft year
        var oldYear = new SchoolYear { Id = 1, YearName = "2026-2027", IsActive = true, Status = SchoolYearStatuses.Active };
        var draft = DraftYear();
        var years = new List<SchoolYear> { oldYear, draft };
        SetupRepository(years);
        ClassRepoMock.Setup(r => r.GetAllSchoolYearsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(years);

        var teacher = new StaffProfile { Id = 7, UserId = 70 };
        var c2A = new Class { Id = 20, ClassName = "2A", SchoolYearId = 2, IsReviewed = true, HomeroomTeacherId = 7, HomeroomTeacher = teacher };
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(2, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class> { c2A });

        var current = new ClassAssignment { Id = 1, ClassId = 10, StudentId = 1, IsActive = true };
        var placed = new ClassAssignment { Id = 2, ClassId = 20, StudentId = 1, IsActive = false, IsDraft = true };
        SetupRepository(new List<ClassAssignment> { current, placed });
        var notifications = new List<Notification>();
        SetupRepository(notifications);

        // Act
        var result = await _service.ActivateSchoolYearAsync(2, 10);

        // Assert: UC-13 step 4, BR-14, BR-17, UC-15 step 5
        result.Success.Should().BeTrue();
        draft.IsActive.Should().BeTrue();
        draft.Status.Should().Be(SchoolYearStatuses.Active);
        oldYear.IsActive.Should().BeFalse();
        oldYear.Status.Should().Be(SchoolYearStatuses.Archived);
        current.IsActive.Should().BeFalse();
        current.EndDate.Should().NotBeNull();
        placed.IsActive.Should().BeTrue();
        placed.IsDraft.Should().BeFalse();
        notifications.Should().ContainSingle(n => n.UserId == 70 && n.Content.Contains("2A"));
        UnitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID04_MarkClassReviewed_DraftClass_RecordsReviewer()
    {
        // Arrange
        var cls = new Class { Id = 1, ClassName = "1A", SchoolYearId = 2, SchoolYear = DraftYear() };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(cls);

        // Act
        var result = await _service.MarkClassReviewedAsync(1, 10);

        // Assert: UC-14 A2
        result.Success.Should().BeTrue();
        result.Data!.IsReviewed.Should().BeTrue();
        cls.ReviewedByUserId.Should().Be(10);
        cls.ReviewedAt.Should().NotBeNull();
        AuditServiceMock.Verify(a => a.LogActionAsync(10, AuditActions.ClassMarkReviewed, nameof(Class), "1",
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID05_MarkClassReviewed_ActiveYear_Fails()
    {
        // Arrange
        var cls = new Class { Id = 1, ClassName = "1A", SchoolYear = new SchoolYear { Status = SchoolYearStatuses.Active, IsActive = true } };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(cls);

        // Act
        var result = await _service.MarkClassReviewedAsync(1, 10);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        cls.IsReviewed.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID06_AssignStudents_DraftYear_CreatesDraftAssignment_ResetsReview()
    {
        // Arrange
        var year = DraftYear();
        var cls = new Class { Id = 1, Capacity = 35, SchoolYearId = 2, SchoolYear = year, IsReviewed = true, ReviewedAt = DateTime.UtcNow, ClassAssignments = new List<ClassAssignment>() };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        var assignments = new List<ClassAssignment>();
        SetupRepository(assignments);

        // Act
        var result = await _service.AssignStudentsToClassAsync(1, new AssignStudentsDto { StudentIds = new List<int> { 5 } }, 10);

        // Assert: UC-16 A1, BR-72 – no student's current class changes
        result.Success.Should().BeTrue();
        assignments.Should().ContainSingle(a => a.StudentId == 5 && a.IsDraft && !a.IsActive && a.StartDate == year.StartDate);
        cls.IsReviewed.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID07_RemoveStudent_DraftAssignment_IsDeleted_ResetsReview()
    {
        // Arrange
        var cls = new Class { Id = 1, SchoolYearId = 2, IsReviewed = true };
        SetupRepository(new List<Class> { cls });
        var draftAssignment = new ClassAssignment { Id = 1, ClassId = 1, StudentId = 10, IsActive = false, IsDraft = true };
        var assignments = new List<ClassAssignment> { draftAssignment };
        SetupRepository(assignments);

        // Act
        var result = await _service.RemoveStudentFromClassAsync(1, 10, 10);

        // Assert
        result.Success.Should().BeTrue();
        assignments.Should().BeEmpty();
        cls.IsReviewed.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID08_UpdateClass_ReviewedDraftClass_ResetsReview()
    {
        // Arrange
        var cls = new Class { Id = 1, ClassName = "1A", Grade = 1, Capacity = 35, Status = "Active", SchoolYear = DraftYear(), IsReviewed = true, ClassAssignments = new List<ClassAssignment>() };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(cls);

        // Act
        var result = await _service.UpdateClassAsync(1, new UpdateClassDto { ClassName = "1A", Grade = 1, Capacity = 30, Status = "Active" }, 10);

        // Assert: BR-72
        result.Success.Should().BeTrue();
        cls.IsReviewed.Should().BeFalse();
        result.Data!.IsReviewed.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID09_DeleteSchoolYear_Archived_Refused()
    {
        // Arrange
        var archived = new SchoolYear { Id = 3, YearName = "2024-2025", IsActive = false, Status = SchoolYearStatuses.Archived };
        SetupRepository(new List<SchoolYear> { archived });

        // Act
        var result = await _service.DeleteSchoolYearAsync(3, 10);

        // Assert: UC-13 A2 – only a Draft year is discarded (BR-52)
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    #endregion
}
