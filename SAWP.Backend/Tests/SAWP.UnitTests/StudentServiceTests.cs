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

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        assignmentRepoMock.Verify(r => r.AddAsync(It.IsAny<ClassAssignment>(), It.IsAny<CancellationToken>()), Times.Never);
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
}
