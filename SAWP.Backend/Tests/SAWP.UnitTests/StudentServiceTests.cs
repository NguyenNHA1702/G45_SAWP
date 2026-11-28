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

public class StudentServiceTests : TestBase
{
    private readonly StudentService _service;

    public StudentServiceTests()
    {
        _service = new StudentService(UnitOfWorkMock.Object, AuditServiceMock.Object);
    }

    #region Method 01: GetStudentProfileAsync (UTCID01 - UTCID06)

    [Fact]
    public async Task UTCID01_GetStudentProfileSuccess_WhenAdminRequests()
    {
        // Arrange
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Nguyen Van A",
            DateOfBirth = new DateTime(2018, 5, 10),
            Gender = "Nam",
            Status = "Active"
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        var result = await _service.GetStudentProfileAsync(1, 10, "OfficeStaff");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.StudentCode.Should().Be("HS001");
        result.Data.FullName.Should().Be("Nguyen Van A");
    }

    [Fact]
    public async Task UTCID02_GetStudentProfileSuccess_WhenParentLinked()
    {
        // Arrange
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Nguyen Van A",
            DateOfBirth = new DateTime(2018, 5, 10),
            Gender = "Nam",
            Status = "Active",
            StudentGuardians = new List<StudentGuardian>
            {
                new()
                {
                    Guardian = new Guardian { UserId = 20 }
                }
            }
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        var result = await _service.GetStudentProfileAsync(1, 20, "Parent");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.StudentCode.Should().Be("HS001");
    }

    [Fact]
    public async Task UTCID03_GetStudentProfileSuccess_WhenTeacherHomeroom()
    {
        // Arrange
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Nguyen Van A",
            Status = "Active",
            ClassAssignments = new List<ClassAssignment>
            {
                new()
                {
                    IsActive = true,
                    Class = new Class
                    {
                        HomeroomTeacher = new StaffProfile { UserId = 30 }
                    }
                }
            }
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        var result = await _service.GetStudentProfileAsync(1, 30, "Teacher");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.FullName.Should().Be("Nguyen Van A");
    }

    [Fact]
    public async Task UTCID04_GetStudentProfileNotFound()
    {
        // Arrange
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        // Act
        var result = await _service.GetStudentProfileAsync(999, 10, "OfficeStaff");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task UTCID05_GetStudentProfileForbidden_WhenParentNotLinked()
    {
        // Arrange
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Nguyen Van A",
            StudentGuardians = new List<StudentGuardian>
            {
                new() { Guardian = new Guardian { UserId = 20 } }
            }
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        var result = await _service.GetStudentProfileAsync(1, 25, "Parent");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID06_GetStudentProfileForbidden_WhenTeacherNotHomeroom()
    {
        // Arrange
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Nguyen Van A",
            ClassAssignments = new List<ClassAssignment>
            {
                new()
                {
                    IsActive = true,
                    Class = new Class { HomeroomTeacher = new StaffProfile { UserId = 30 } }
                }
            }
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        var result = await _service.GetStudentProfileAsync(1, 35, "Teacher");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    #endregion

    #region Method 02: GetStudentsPagedAsync (UTCID01 - UTCID04)

    [Fact]
    public async Task UTCID01_GetStudentsPaged_DefaultFilter_ReturnsAll()
    {
        // Arrange
        var students = new List<Student>
        {
            new() { Id = 1, StudentCode = "HS001", FullName = "Nguyen Van A", Status = "Active", CreatedAt = DateTime.UtcNow },
            new() { Id = 2, StudentCode = "HS002", FullName = "Tran Thi B", Status = "Active", CreatedAt = DateTime.UtcNow }
        };
        SetupRepository(students);

        var filter = new StudentFilterDto { PageIndex = 1, PageSize = 10 };

        // Act
        var result = await _service.GetStudentsPagedAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(2);
        result.Data.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID02_GetStudentsPaged_WithSearchTerm_FiltersCorrectly()
    {
        // Arrange
        var students = new List<Student>
        {
            new() { Id = 1, StudentCode = "HS001", FullName = "Nguyen Van A", Status = "Active", CreatedAt = DateTime.UtcNow },
            new() { Id = 2, StudentCode = "HS002", FullName = "Tran Thi B", Status = "Active", CreatedAt = DateTime.UtcNow }
        };
        SetupRepository(students);

        var filter = new StudentFilterDto { SearchTerm = "Tran", PageIndex = 1, PageSize = 10 };

        // Act
        var result = await _service.GetStudentsPagedAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items.First().FullName.Should().Be("Tran Thi B");
    }

    [Fact]
    public async Task UTCID05_GetStudentsPaged_WithMultipleSpacesInSearchTerm_FiltersCorrectly()
    {
        // Arrange
        var students = new List<Student>
        {
            new() { Id = 1, StudentCode = "HS001", FullName = "Nguyen Van A", Status = "Active", CreatedAt = DateTime.UtcNow },
            new() { Id = 2, StudentCode = "HS002", FullName = "Phan Thu Hương", Status = "Active", CreatedAt = DateTime.UtcNow }
        };
        SetupRepository(students);

        var filter = new StudentFilterDto { SearchTerm = "  Phan   Thu   Hương  ", PageIndex = 1, PageSize = 10 };

        // Act
        var result = await _service.GetStudentsPagedAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items.First().FullName.Should().Be("Phan Thu Hương");
    }

    [Fact]
    public async Task UTCID03_GetStudentsPaged_WithClassFilter_FiltersCorrectly()
    {
        // Arrange
        var students = new List<Student>
        {
            new()
            {
                Id = 1,
                StudentCode = "HS001",
                FullName = "Nguyen Van A",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                ClassAssignments = new List<ClassAssignment>
                {
                    new() { ClassId = 1, IsActive = true, Class = new Class { Id = 1, Grade = 1, SchoolYearId = 1 } }
                }
            },
            new()
            {
                Id = 2,
                StudentCode = "HS002",
                FullName = "Tran Thi B",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                ClassAssignments = new List<ClassAssignment>
                {
                    new() { ClassId = 2, IsActive = true, Class = new Class { Id = 2, Grade = 2, SchoolYearId = 1 } }
                }
            }
        };
        SetupRepository(students);

        var filter = new StudentFilterDto { ClassId = 1, PageIndex = 1, PageSize = 10 };

        // Act
        var result = await _service.GetStudentsPagedAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items.First().StudentCode.Should().Be("HS001");
    }

    [Fact]
    public async Task UTCID04_GetStudentsPaged_OutOfRangePage_ReturnsEmptyItems()
    {
        // Arrange
        var students = new List<Student>
        {
            new() { Id = 1, StudentCode = "HS001", FullName = "Nguyen Van A", Status = "Active", CreatedAt = DateTime.UtcNow }
        };
        SetupRepository(students);

        var filter = new StudentFilterDto { PageIndex = 999, PageSize = 10 };

        // Act
        var result = await _service.GetStudentsPagedAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items.Should().BeEmpty();
    }

    #endregion

    #region Method 03: CreateStudentAsync (UTCID01 - UTCID05)

    [Fact]
    public async Task UTCID01_CreateStudent_Success_BasicProfile()
    {
        // Arrange
        var request = new CreateStudentDto
        {
            StudentCode = "HS001",
            FullName = "Nguyen Van A",
            DateOfBirth = new DateTime(2018, 1, 1),
            Gender = "Nam"
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        StudentRepoMock.Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => { s.Id = 1; return s; });
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 1, StudentCode = "HS001", FullName = "Nguyen Van A", Status = "Active" });

        // Act
        var result = await _service.CreateStudentAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        result.Data!.StudentCode.Should().Be("HS001");
    }

    [Fact]
    public async Task UTCID02_CreateStudent_Success_WithClassAndGuardian()
    {
        // Arrange
        var request = new CreateStudentDto
        {
            StudentCode = "HS002",
            FullName = "Tran Thi B",
            DateOfBirth = new DateTime(2018, 3, 15),
            Gender = "Nữ",
            InitialClassId = 1,
            InitialGuardian = new GuardianInputDto
            {
                FullName = "Tran Van Bo",
                PhoneNumber = "0901234567",
                Relationship = 1
            }
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS002", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        StudentRepoMock.Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => { s.Id = 2; return s; });

        var targetClass = new Class
        {
            Id = 1,
            ClassName = "1A",
            Capacity = 35,
            ClassAssignments = new List<ClassAssignment>()
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetClass);

        SetupRepository(new List<User>());
        SetupRepository(new List<Role> { new() { Id = 3, Name = UserRoles.Parent.ToString() } });
        SetupRepository(new List<UserRole>());
        SetupRepository(new List<Guardian>());
        SetupRepository(new List<StudentGuardian>());
        SetupRepository(new List<ClassAssignment>());

        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 2, StudentCode = "HS002", FullName = "Tran Thi B", Status = "Active" });

        // Act
        var result = await _service.CreateStudentAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
    }

    [Fact]
    public async Task UTCID03_CreateStudent_Fail_DuplicateCode()
    {
        // Arrange
        var request = new CreateStudentDto
        {
            StudentCode = "HS001",
            FullName = "Nguyen Van Duplicate",
            Gender = "Nam"
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CreateStudentAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG12.Code);
    }

    [Fact]
    public async Task UTCID04_CreateStudent_Fail_MissingRequiredFields()
    {
        // Arrange
        var request = new CreateStudentDto
        {
            StudentCode = "",
            FullName = ""
        };

        // Act
        var result = await _service.CreateStudentAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID14_CreateStudent_Fail_FutureDateOfBirth()
    {
        var request = new CreateStudentDto
        {
            StudentCode = "HS_FUT",
            FullName = "Nguyen Future",
            DateOfBirth = DateTime.UtcNow.Date.AddDays(10),
            Gender = "Nam"
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS_FUT", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateStudentAsync(request, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("tương lai"));
    }

    [Fact]
    public async Task UTCID15_CreateStudent_Fail_AgeBelow5()
    {
        var request = new CreateStudentDto
        {
            StudentCode = "HS_BABY",
            FullName = "Nguyen Baby",
            DateOfBirth = DateTime.UtcNow.Date.AddYears(-3),
            Gender = "Nam"
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS_BABY", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateStudentAsync(request, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("5 đến 15"));
    }

    [Fact]
    public async Task UTCID16_CreateStudent_Fail_AgeAbove15()
    {
        var request = new CreateStudentDto
        {
            StudentCode = "HS_OLD",
            FullName = "Nguyen Old",
            DateOfBirth = DateTime.UtcNow.Date.AddYears(-20),
            Gender = "Nam"
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS_OLD", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateStudentAsync(request, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("5 đến 15"));
    }

    [Fact]
    public async Task UTCID17_CreateStudent_Fail_GuardianPhoneNot10Digits()
    {
        var request = new CreateStudentDto
        {
            StudentCode = "HS_PHONE_9",
            FullName = "Nguyen Test Phone",
            DateOfBirth = DateTime.UtcNow.Date.AddYears(-7),
            Gender = "Nam",
            InitialGuardian = new GuardianInputDto
            {
                FullName = "Phu Huynh A",
                PhoneNumber = "091234567" // 9 digits
            }
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS_PHONE_9", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateStudentAsync(request, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("10 chữ số"));
    }

    [Fact]
    public async Task UTCID18_CreateStudent_Fail_GuardianPhoneNotStartingWithZero()
    {
        var request = new CreateStudentDto
        {
            StudentCode = "HS_PHONE_NZ",
            FullName = "Nguyen Test Phone",
            DateOfBirth = DateTime.UtcNow.Date.AddYears(-7),
            Gender = "Nam",
            InitialGuardian = new GuardianInputDto
            {
                FullName = "Phu Huynh A",
                PhoneNumber = "1912345678" // 10 digits but starts with 1
            }
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS_PHONE_NZ", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateStudentAsync(request, 10);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Errors.Should().Contain(e => e.Contains("10 chữ số"));
    }

    [Fact]
    public async Task UTCID05_CreateStudent_Boundary_ClassAtFullCapacity_StudentCreatedWithoutAssignment()
    {
        // Arrange
        var request = new CreateStudentDto
        {
            StudentCode = "HS003",
            FullName = "Le Van C",
            DateOfBirth = new DateTime(2018, 2, 2),
            Gender = "Nam",
            InitialClassId = 1
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS003", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        StudentRepoMock.Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => { s.Id = 3; return s; });

        // Class has 35 assignments and capacity 35
        var fullAssignments = Enumerable.Range(1, 35).Select(i => new ClassAssignment { IsActive = true }).ToList();
        var fullClass = new Class
        {
            Id = 1,
            Capacity = 35,
            ClassAssignments = fullAssignments
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fullClass);

        var assignmentRepoMock = SetupRepository(new List<ClassAssignment>());

        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 3, StudentCode = "HS003", FullName = "Le Van C", Status = "Active" });

        // Act
        var result = await _service.CreateStudentAsync(request, 10);

        // Assert (SC-10: Reject creating student into a full class instead of silently leaving unassigned)
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG15.Code);
        assignmentRepoMock.Verify(r => r.AddAsync(It.IsAny<ClassAssignment>(), It.IsAny<CancellationToken>()), Times.Never);
        StudentRepoMock.Verify(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Method 04: UpdateStudentAsync (UTCID01 - UTCID03)

    [Fact]
    public async Task UTCID01_UpdateStudent_Success_BasicInfo()
    {
        // Arrange
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Nguyen Van A Old",
            Gender = "Nam",
            Status = "Active"
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        var request = new UpdateStudentDto
        {
            FullName = "Nguyen Van A New",
            DateOfBirth = new DateTime(2018, 5, 20),
            Gender = "Nam",
            Status = "Active"
        };

        // Act
        var result = await _service.UpdateStudentAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        StudentRepoMock.Verify(r => r.UpdateAsync(It.Is<Student>(s => s.FullName == "Nguyen Van A New"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID02_UpdateStudent_Success_ChangeClass()
    {
        // Arrange
        var oldAssignment = new ClassAssignment { Id = 10, ClassId = 1, IsActive = true };
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Nguyen Van A",
            Gender = "Nam",
            Status = "Active",
            ClassAssignments = new List<ClassAssignment> { oldAssignment }
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        var targetClass = new Class { Id = 2, ClassName = "1B", Capacity = 35 };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetClass);

        var assignmentRepoMock = SetupRepository(new List<ClassAssignment> { oldAssignment });

        var request = new UpdateStudentDto
        {
            FullName = "Nguyen Van A",
            DateOfBirth = new DateTime(2018, 5, 20),
            Gender = "Nam",
            Status = "Active",
            CurrentClassId = 2
        };

        // Act
        var result = await _service.UpdateStudentAsync(1, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        oldAssignment.IsActive.Should().BeFalse();
        assignmentRepoMock.Verify(r => r.AddAsync(It.Is<ClassAssignment>(ca => ca.ClassId == 2 && ca.IsActive), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID03_UpdateStudent_Fail_NotFound()
    {
        // Arrange
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        var request = new UpdateStudentDto { FullName = "Ghost", Gender = "Nam", Status = "Active" };

        // Act
        var result = await _service.UpdateStudentAsync(999, request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    #endregion

    #region Method 05: CheckDuplicateAsync (UTCID01 - UTCID03)

    [Fact]
    public async Task UTCID01_CheckDuplicate_WhenCodeDoesNotExist_ReturnsNotDuplicate()
    {
        // Arrange
        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS_NEW", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        // Act
        var result = await _service.CheckDuplicateAsync("HS_NEW");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.IsDuplicate.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID02_CheckDuplicate_WhenCodeExists_ReturnsDuplicate()
    {
        // Arrange
        var existing = new Student { Id = 1, StudentCode = "HS_EXIST", FullName = "Tran Van Existing" };
        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS_EXIST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _service.CheckDuplicateAsync("HS_EXIST");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.IsDuplicate.Should().BeTrue();
        result.Data.ExistingStudent.Should().NotBeNull();
        result.Code.Should().Be(SystemMessages.MSG12.Code);
    }

    [Fact]
    public async Task UTCID03_CheckDuplicate_WhenCodeIsEmpty_ReturnsFail()
    {
        // Arrange & Act
        var result = await _service.CheckDuplicateAsync("");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 06: ResolveDuplicateAsync (UTCID01 - UTCID06)

    [Fact]
    public async Task UTCID01_ResolveDuplicate_ActionRename_Success()
    {
        // Arrange
        var request = new ResolveDuplicateStudentDto
        {
            StudentCode = "HS001",
            Action = "Rename",
            NewStudentCode = "HS001_NEW",
            ProfileData = new CreateStudentDto
            {
                FullName = "Nguyen Van Renamed",
                DateOfBirth = new DateTime(2018, 1, 1),
                Gender = "Nam"
            }
        };
        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 1, StudentCode = "HS001" });
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS001_NEW", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        StudentRepoMock.Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => { s.Id = 2; return s; });
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 2, StudentCode = "HS001_NEW", FullName = "Nguyen Van Renamed", Status = "Active" });

        // Act
        var result = await _service.ResolveDuplicateAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.StudentCode.Should().Be("HS001_NEW");
    }

    [Fact]
    public async Task UTCID02_ResolveDuplicate_ActionMerge_Success()
    {
        // Arrange
        var existing = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Tran A",
            Gender = "Nam",
            Status = "Inactive"
        };
        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new ResolveDuplicateStudentDto
        {
            StudentCode = "HS001",
            Action = "Merge",
            ProfileData = new CreateStudentDto
            {
                FullName = "Tran A Updated",
                Gender = "Nam"
            }
        };

        // Act
        var result = await _service.ResolveDuplicateAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        existing.FullName.Should().Be("Tran A Updated");
        existing.Status.Should().Be("Active");
    }

    [Fact]
    public async Task UTCID03_ResolveDuplicate_ActionOverwrite_Success()
    {
        // Arrange
        var existing = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Old Data",
            Gender = "Nam"
        };
        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new ResolveDuplicateStudentDto
        {
            StudentCode = "HS001",
            Action = "Overwrite",
            ProfileData = new CreateStudentDto
            {
                FullName = "Overwritten Name",
                Gender = "Nữ"
            }
        };

        // Act
        var result = await _service.ResolveDuplicateAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        existing.FullName.Should().Be("Overwritten Name");
        existing.Gender.Should().Be("Nữ");
    }

    [Fact]
    public async Task UTCID04_ResolveDuplicate_ActionRename_MissingNewCode_Fails()
    {
        // Arrange
        var request = new ResolveDuplicateStudentDto
        {
            StudentCode = "HS001",
            Action = "Rename",
            NewStudentCode = ""
        };

        // Act
        var result = await _service.ResolveDuplicateAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID05_ResolveDuplicate_ActionRename_NewCodeExists_Fails()
    {
        // Arrange
        var request = new ResolveDuplicateStudentDto
        {
            StudentCode = "HS001",
            Action = "Rename",
            NewStudentCode = "HS002"
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS002", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ResolveDuplicateAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG12.Code);
    }

    [Fact]
    public async Task UTCID06_ResolveDuplicate_MissingOriginalStudentCode_Fails()
    {
        // Arrange
        var request = new ResolveDuplicateStudentDto
        {
            StudentCode = "",
            Action = "Merge"
        };

        // Act
        var result = await _service.ResolveDuplicateAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 07: ActivateStudentAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_ActivateStudent_Success()
    {
        // Arrange
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            Status = "Draft"
        };
        SetupRepository(new List<Student> { student });

        // Act
        var result = await _service.ActivateStudentAsync(1, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        student.Status.Should().Be("Active");
    }

    [Fact]
    public async Task UTCID02_ActivateStudent_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<Student>());

        // Act
        var result = await _service.ActivateStudentAsync(999, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    #endregion

    #region Method 08: guardian records without accounts (SSAWP-151, BR-64) (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetStudentProfile_GuardianDetailsComeFromGuardianRecord()
    {
        // Arrange: one guardian record without an account, one linked to an account whose own details differ
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Nguyen Van A",
            Status = "Active",
            StudentGuardians = new List<StudentGuardian>
            {
                new()
                {
                    GuardianId = 5,
                    IsPrimaryContact = true,
                    Relationship = RelationshipTypes.Mother,
                    Guardian = new Guardian { Id = 5, UserId = null, FullName = "Tran Thi Me", PhoneNumber = "0901111111", Email = "me@example.com" }
                },
                new()
                {
                    GuardianId = 6,
                    Relationship = RelationshipTypes.Father,
                    Guardian = new Guardian
                    {
                        Id = 6, UserId = 20, FullName = "Nguyen Van Bo", PhoneNumber = "0902222222", Email = "bo@example.com",
                        User = new User { Id = 20, FullName = "Account Name", PhoneNumber = "0999999999", Email = "login@example.com" }
                    }
                }
            }
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(student);

        // Act
        var result = await _service.GetStudentProfileAsync(1, 10, "OfficeStaff");

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.PrimaryGuardianName.Should().Be("Tran Thi Me");
        result.Data.PrimaryGuardianPhone.Should().Be("0901111111");
        var mother = result.Data.Guardians.Single(g => g.GuardianId == 5);
        mother.UserId.Should().BeNull();
        mother.FullName.Should().Be("Tran Thi Me");
        mother.Email.Should().Be("me@example.com");
        var father = result.Data.Guardians.Single(g => g.GuardianId == 6);
        father.UserId.Should().Be(20);
        father.FullName.Should().Be("Nguyen Van Bo");
        father.PhoneNumber.Should().Be("0902222222");
        father.Email.Should().Be("bo@example.com");
    }

    [Fact]
    public async Task UTCID02_CreateStudent_NewGuardian_CreatesRecordWithoutAccount()
    {
        // Arrange
        var request = new CreateStudentDto
        {
            StudentCode = "HS002",
            FullName = "Tran Thi B",
            DateOfBirth = new DateTime(2018, 3, 15),
            Gender = "Nữ",
            InitialGuardian = new GuardianInputDto { FullName = "Tran Van Bo", PhoneNumber = "0901234567", Relationship = 1 }
        };
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS002", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        StudentRepoMock.Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => { s.Id = 2; return s; });
        var users = new List<User>();
        var guardians = new List<Guardian>();
        SetupRepository(users);
        SetupRepository(new List<Role> { new() { Id = 3, Name = UserRoles.Parent.ToString() } });
        SetupRepository(new List<UserRole>());
        SetupRepository(guardians);
        SetupRepository(new List<StudentGuardian>());
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 2, StudentCode = "HS002", FullName = "Tran Thi B", Status = "Active" });

        // Act
        var result = await _service.CreateStudentAsync(request, 10);

        // Assert
        result.Success.Should().BeTrue();
        var guardian = guardians.Should().ContainSingle().Subject;
        guardian.FullName.Should().Be("Tran Van Bo");
        guardian.PhoneNumber.Should().Be("0901234567");
        guardian.Email.Should().BeNull();
        guardian.UserId.Should().BeNull();
        users.Should().BeEmpty("a guardian record never creates a user account (SSAWP-152, BR-66)");
    }

    #endregion

    #region Method 09: guardian records and UC-10 (SSAWP-152, UC-09 A3, BR-13, BR-66) (UTCID01 - UTCID09)

    private (List<Guardian> Guardians, List<StudentGuardian> Links, Student Student) SetupStudentWithGuardians(string status = "Active")
    {
        var student = new Student { Id = 1, StudentCode = "HS001", FullName = "Nguyen Van A", Status = status };
        var mother = new Guardian { Id = 5, FullName = "Tran Thi Me", Email = "me@example.com", IdentityCardNumber = "001100000005" };
        var father = new Guardian { Id = 6, FullName = "Nguyen Van Bo", PhoneNumber = "0902222222" };
        var other = new Guardian { Id = 7, FullName = "Le Van Chu", Email = "chu@example.com", IdentityCardNumber = "001100000007" };
        var guardians = new List<Guardian> { mother, father, other };
        var links = new List<StudentGuardian>
        {
            new() { StudentId = 1, GuardianId = 5, Guardian = mother, Relationship = RelationshipTypes.Mother, IsPrimaryContact = true },
            new() { StudentId = 1, GuardianId = 6, Guardian = father, Relationship = RelationshipTypes.Father, IsPrimaryContact = false }
        };
        student.StudentGuardians = links;
        SetupRepository(new List<Student> { student });
        SetupRepository(guardians);
        SetupRepository(links);
        // Like the database reload: each link comes back with its guardian record
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(() =>
        {
            foreach (var link in links.Where(l => l.Guardian == null))
            {
                link.Guardian = guardians.First(g => g.Id == link.GuardianId);
            }
            return student;
        });
        return (guardians, links, student);
    }

    [Fact]
    public async Task UTCID01_LinkGuardian_ExistingRecord_AddsNonPrimaryLink()
    {
        var (_, links, _) = SetupStudentWithGuardians();

        var result = await _service.LinkGuardianAsync(1, new LinkGuardianRequestDto { GuardianId = 7, Relationship = 3 }, 10);

        result.Code.Should().Be(SystemMessages.MSG08.Code);
        links.Should().ContainSingle(l => l.GuardianId == 7).Which.IsPrimaryContact.Should().BeFalse();
        links.Single(l => l.GuardianId == 5).IsPrimaryContact.Should().BeTrue();
        AuditServiceMock.Verify(a => a.LogActionAsync(10, AuditActions.StudentGuardianLink, It.IsAny<string>(), "1:7",
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID02_LinkGuardian_NewGuardianWithKnownEmail_ReusesRecord_NoAccount()
    {
        var (guardians, links, _) = SetupStudentWithGuardians();
        var users = new List<User>();
        SetupRepository(users);

        var result = await _service.LinkGuardianAsync(1, new LinkGuardianRequestDto
        {
            NewGuardian = new GuardianInputDto { FullName = "Le Van Chu", Email = "CHU@example.com", PhoneNumber = "0999999999" },
            Relationship = 3
        });

        result.Success.Should().BeTrue();
        guardians.Should().HaveCount(3, "the record with the same email is reused");
        links.Should().Contain(l => l.GuardianId == 7);
        guardians.Single(g => g.Id == 7).PhoneNumber.Should().Be("0999999999", "only an empty field is filled");
        users.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID03_LinkGuardian_SamePhoneOnly_CreatesNewRecord()
    {
        var (guardians, _, _) = SetupStudentWithGuardians();

        var result = await _service.LinkGuardianAsync(1, new LinkGuardianRequestDto
        {
            NewGuardian = new GuardianInputDto { FullName = "Nguoi Khac", PhoneNumber = "0902222222" },
            Relationship = 4
        });

        result.Success.Should().BeTrue();
        guardians.Should().HaveCount(4, "a phone number never matches a record (BR-66)");
    }

    [Fact]
    public async Task UTCID04_LinkGuardian_AlreadyLinked_ReturnsMSG01()
    {
        SetupStudentWithGuardians();

        var result = await _service.LinkGuardianAsync(1, new LinkGuardianRequestDto { GuardianId = 6, Relationship = 1 });

        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID05_LinkGuardian_IdentityCardAndEmailOfDifferentRecords_ReturnsMSG01()
    {
        var (guardians, _, _) = SetupStudentWithGuardians();

        var result = await _service.LinkGuardianAsync(1, new LinkGuardianRequestDto
        {
            NewGuardian = new GuardianInputDto { FullName = "Phu Huynh X", IdentityCardNumber = "001100000005", Email = "chu@example.com" },
            Relationship = 3
        });

        result.Code.Should().Be(SystemMessages.MSG01.Code);
        guardians.Should().HaveCount(3);
    }

    [Fact]
    public async Task UTCID06_UnlinkGuardian_PrimaryOfActiveStudent_ReturnsMSG01()
    {
        var (_, links, _) = SetupStudentWithGuardians();

        var result = await _service.UnlinkGuardianAsync(1, 5);

        result.Code.Should().Be(SystemMessages.MSG01.Code);
        links.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID07_UnlinkGuardian_NonPrimary_RemovesLinkButKeepsRecord()
    {
        var (guardians, links, _) = SetupStudentWithGuardians();

        var result = await _service.UnlinkGuardianAsync(1, 6, 10);

        result.Code.Should().Be(SystemMessages.MSG08.Code);
        links.Should().NotContain(l => l.GuardianId == 6);
        guardians.Should().Contain(g => g.Id == 6);
    }

    [Fact]
    public async Task UTCID08_UpdateGuardianLink_MakePrimary_MovesPrimaryFromOtherGuardian()
    {
        var (_, links, _) = SetupStudentWithGuardians();

        var result = await _service.UpdateGuardianLinkAsync(1, 6, new UpdateGuardianLinkRequestDto { Relationship = 1, IsPrimaryContact = true });

        result.Success.Should().BeTrue();
        links.Single(l => l.GuardianId == 6).IsPrimaryContact.Should().BeTrue();
        links.Single(l => l.GuardianId == 5).IsPrimaryContact.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID09_UpdateGuardianLink_RemovePrimaryFlagOfOnlyPrimary_ReturnsMSG01()
    {
        var (_, links, _) = SetupStudentWithGuardians();

        var result = await _service.UpdateGuardianLinkAsync(1, 5, new UpdateGuardianLinkRequestDto { Relationship = 2, IsPrimaryContact = false });

        result.Code.Should().Be(SystemMessages.MSG01.Code);
        links.Single(l => l.GuardianId == 5).IsPrimaryContact.Should().BeTrue();
    }

    [Fact]
    public async Task SC07_CreateStudent_NameWithDigits_FailsWithMSG01()
    {
        var result = await _service.CreateStudentAsync(new CreateStudentDto
        {
            StudentCode = "TEST-N25-G1",
            FullName = "Nguyen 123",
            DateOfBirth = new DateTime(2019, 5, 1),
            Gender = "Nam"
        });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task SC07_CreateStudent_GuardianNameWithDigits_FailsWithMSG01()
    {
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("TEST-N25-G2", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateStudentAsync(new CreateStudentDto
        {
            StudentCode = "TEST-N25-G2",
            FullName = "Nguyen Van An",
            DateOfBirth = new DateTime(2019, 5, 1),
            Gender = "Nam",
            InitialGuardian = new GuardianInputDto { FullName = "Nguyen 123", PhoneNumber = "0912345678", Relationship = 1 }
        });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task SC09_UpdateStudent_AgeMismatchWithCurrentClass_FailsWithMSG01()
    {
        var grade1Class = new Class { Id = 1, ClassName = "1A", Grade = 1, SchoolYear = new SchoolYear { StartDate = new DateTime(2026, 9, 1) } };
        var student = new Student
        {
            Id = 10,
            StudentCode = "HS010",
            FullName = "Em A",
            DateOfBirth = new DateTime(2020, 5, 1),
            Gender = "Nam",
            Status = "Active",
            ClassAssignments = new List<ClassAssignment> { new() { Id = 1, ClassId = 1, Class = grade1Class, IsActive = true } }
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Attempt to change DOB so student is 14 years old while in Grade 1 (without passing CurrentClassId)
        var result = await _service.UpdateStudentAsync(10, new UpdateStudentDto
        {
            FullName = "Em A",
            DateOfBirth = new DateTime(2012, 5, 1),
            Gender = "Nam",
            Status = "Active"
        });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task SC10_CreateStudent_FullInitialClass_FailsWithMSG15()
    {
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS_FULL_TEST", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var fullClass = new Class
        {
            Id = 1,
            ClassName = "1A",
            Grade = 1,
            Capacity = 2,
            SchoolYear = new SchoolYear { StartDate = new DateTime(2026, 9, 1), YearName = "2026-2027" },
            ClassAssignments = new List<ClassAssignment> { new() { IsActive = true }, new() { IsActive = true } }
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fullClass);

        var result = await _service.CreateStudentAsync(new CreateStudentDto
        {
            StudentCode = "HS_FULL_TEST",
            FullName = "Nguyen Van An",
            DateOfBirth = new DateTime(2020, 5, 1),
            Gender = "Nam",
            InitialClassId = 1
        });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG15.Code);
    }

    #endregion

    #region Phase 4 Fixes (BUG-STU-01..05, GAP-02)

    [Fact]
    public async Task BUG_STU_01_CreateStudent_EmptyDob_And_1CharName_Fail()
    {
        var emptyDobResult = await _service.CreateStudentAsync(new CreateStudentDto
        {
            StudentCode = "HS_NO_DOB",
            FullName = "Nguyen Van An",
            Gender = "Nam"
        });
        emptyDobResult.Success.Should().BeFalse();
        emptyDobResult.Code.Should().Be(SystemMessages.MSG01.Code);

        var shortNameResult = await _service.CreateStudentAsync(new CreateStudentDto
        {
            StudentCode = "HS_SHORT",
            FullName = "A",
            DateOfBirth = new DateTime(2019, 1, 1),
            Gender = "Nam"
        });
        shortNameResult.Success.Should().BeFalse();
        shortNameResult.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task BUG_STU_01_CreateStudent_OverAge15_SucceedsOnlyWhenAllowAgeExceptionTrue()
    {
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS_OVER60", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        StudentRepoMock.Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => { s.Id = 99; return s; });
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 99, StudentCode = "HS_OVER60", FullName = "Nguyen Van Lon", Status = "Active" });

        var blocked = await _service.CreateStudentAsync(new CreateStudentDto
        {
            StudentCode = "HS_OVER60",
            FullName = "Nguyen Van Lon",
            DateOfBirth = DateTime.UtcNow.Date.AddYears(-60),
            Gender = "Nam",
            AllowAgeException = false
        });
        blocked.Success.Should().BeFalse();
        blocked.Code.Should().Be(SystemMessages.MSG01.Code);

        var allowed = await _service.CreateStudentAsync(new CreateStudentDto
        {
            StudentCode = "HS_OVER60",
            FullName = "Nguyen Van Lon",
            DateOfBirth = DateTime.UtcNow.Date.AddYears(-60),
            Gender = "Nam",
            AllowAgeException = true,
            AgeExceptionReason = "Xac nhan ngoai le"
        }, 10);
        allowed.Success.Should().BeTrue();
    }

    [Fact]
    public async Task BUG_STU_02_CreateStudent_DraftAndArchivedSchoolYearBehavior()
    {
        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        StudentRepoMock.Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => { s.Id = 50; return s; });
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 50, StudentCode = "HS_DRAFT", FullName = "Nguyen Draft", Status = "Active" });
        var assignmentRepo = SetupRepository(new List<ClassAssignment>());

        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Class
            {
                Id = 20,
                ClassName = "1A",
                Grade = 1,
                Capacity = 35,
                SchoolYear = new SchoolYear { YearName = "2027-2028", Status = SchoolYearStatuses.Draft, StartDate = new DateTime(2027, 9, 1) },
                ClassAssignments = new List<ClassAssignment>()
            });

        var draftResult = await _service.CreateStudentAsync(new CreateStudentDto
        {
            StudentCode = "HS_DRAFT",
            FullName = "Nguyen Draft",
            DateOfBirth = new DateTime(2020, 5, 1),
            Gender = "Nam",
            InitialClassId = 20
        }, 10);

        draftResult.Success.Should().BeTrue();
        assignmentRepo.Verify(r => r.AddAsync(It.Is<ClassAssignment>(ca => ca.ClassId == 20 && ca.IsDraft && !ca.IsActive), It.IsAny<CancellationToken>()), Times.Once);

        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Class
            {
                Id = 21,
                ClassName = "1B",
                Grade = 1,
                Capacity = 35,
                SchoolYear = new SchoolYear { YearName = "2024-2025", Status = SchoolYearStatuses.Archived, StartDate = new DateTime(2024, 9, 1) },
                ClassAssignments = new List<ClassAssignment>()
            });

        var archivedResult = await _service.CreateStudentAsync(new CreateStudentDto
        {
            StudentCode = "HS_ARCH",
            FullName = "Nguyen Arch",
            DateOfBirth = new DateTime(2018, 5, 1),
            Gender = "Nam",
            InitialClassId = 21
        }, 10);

        archivedResult.Success.Should().BeFalse();
        archivedResult.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task BUG_STU_03_ResolveDuplicate_ValidatesNameAndCapacity()
    {
        var existing = new Student { Id = 1, StudentCode = "HS001", FullName = "Old Name", DateOfBirth = new DateTime(2019, 1, 1), Gender = "Nam", Status = "Active" };
        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS001", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var badName = await _service.ResolveDuplicateAsync(new ResolveDuplicateStudentDto
        {
            StudentCode = "HS001",
            Action = "Overwrite",
            ProfileData = new CreateStudentDto { FullName = "123Invalid", Gender = "Nam" }
        });
        badName.Success.Should().BeFalse();

        var fullClass = new Class
        {
            Id = 30,
            ClassName = "2A",
            Grade = 2,
            Capacity = 1,
            SchoolYear = new SchoolYear { YearName = "2026-2027", Status = SchoolYearStatuses.Active, StartDate = new DateTime(2026, 9, 1) },
            ClassAssignments = new List<ClassAssignment> { new() { IsActive = true } }
        };
        ClassRepoMock.Setup(r => r.GetClassWithDetailsAsync(30, It.IsAny<CancellationToken>())).ReturnsAsync(fullClass);

        var fullResult = await _service.ResolveDuplicateAsync(new ResolveDuplicateStudentDto
        {
            StudentCode = "HS001",
            Action = "Merge",
            ProfileData = new CreateStudentDto { FullName = "Valid Name", DateOfBirth = new DateTime(2019, 1, 1), Gender = "Nam", InitialClassId = 30 }
        });
        fullResult.Success.Should().BeFalse();
        fullResult.Code.Should().Be(SystemMessages.MSG15.Code);
    }

    [Fact]
    public async Task BUG_STU_04_GuardianInvalidCccdFails_And_UpdateGuardianContactWorks()
    {
        var (guardians, _, _) = SetupStudentWithGuardians();

        var badCccd = await _service.LinkGuardianAsync(1, new LinkGuardianRequestDto
        {
            NewGuardian = new GuardianInputDto { FullName = "Le Van B", IdentityCardNumber = "12345" },
            Relationship = 3
        });
        badCccd.Success.Should().BeFalse();
        badCccd.Code.Should().Be(SystemMessages.MSG01.Code);

        var updated = await _service.UpdateGuardianContactAsync(6, new UpdateGuardianContactDto
        {
            FullName = "Nguyen Van Chu Updated",
            PhoneNumber = "0912345678",
            Email = "chu.new@example.com",
            IdentityCardNumber = "001200999888"
        }, 10);

        updated.Success.Should().BeTrue();
        guardians.Single(g => g.Id == 6).PhoneNumber.Should().Be("0912345678");
    }

    [Fact]
    public async Task GAP_02_GetStudentHistory_RespectsScopeAndAggregatesData()
    {
        var student = new Student
        {
            Id = 70,
            StudentCode = "HS070",
            FullName = "Pham Gia Huy",
            Status = "Active",
            ClassAssignments = new List<ClassAssignment>
            {
                new() { Id = 1, ClassId = 1, AcademicYear = "2026-2027", IsActive = true, Class = new Class { Id = 1, ClassName = "1A", Grade = 1, HomeroomTeacher = new StaffProfile { UserId = 200 } } }
            },
            StudentGuardians = new List<StudentGuardian>
            {
                new() { Guardian = new Guardian { Id = 9, UserId = 300, FullName = "Pham Van Cha" } }
            }
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(70, It.IsAny<CancellationToken>())).ReturnsAsync(student);
        SetupRepository(new List<AttendanceRecord> { new() { StudentId = 70, ClassId = 1, Date = DateTime.UtcNow.Date, Status = AttendanceStatus.Present } });
        SetupRepository(new List<AdministrativeRequest>());
        SetupRepository(new List<LunchRegistration> { new() { StudentId = 70, Date = DateTime.UtcNow.Date, IsActive = true } });
        SetupRepository(new List<AuditLog> { new() { EntityName = nameof(Student), EntityId = "70", Action = AuditActions.StudentCreate, Timestamp = DateTime.UtcNow } });

        var deniedAdmin = await _service.GetStudentHistoryAsync(70, 1, UserRoles.Admin.ToString());
        deniedAdmin.Success.Should().BeFalse();
        deniedAdmin.Code.Should().Be(SystemMessages.MSG10.Code);

        var deniedOtherTeacher = await _service.GetStudentHistoryAsync(70, 999, UserRoles.Teacher.ToString());
        deniedOtherTeacher.Success.Should().BeFalse();

        var okParent = await _service.GetStudentHistoryAsync(70, 300, UserRoles.Parent.ToString());
        okParent.Success.Should().BeTrue();
        okParent.Data!.AttendanceSummary.PresentCount.Should().Be(1);
        okParent.Data.AuditLogs.Should().BeEmpty();

        var okOffice = await _service.GetStudentHistoryAsync(70, 10, UserRoles.OfficeStaff.ToString());
        okOffice.Success.Should().BeTrue();
        okOffice.Data!.AuditLogs.Should().HaveCount(1);
    }

    #endregion
}
