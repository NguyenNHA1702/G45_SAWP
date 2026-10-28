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

public class GuardianServiceTests : TestBase
{
    private readonly GuardianService _service;

    public GuardianServiceTests()
    {
        _service = new GuardianService(UnitOfWorkMock.Object, AuditServiceMock.Object);
        SetupRepository(new List<Notification>());
    }

    #region Method 28: SubmitChildLinkRequestAsync (UTCID01 - UTCID06)

    [Fact]
    public async Task UTCID01_SubmitChildLinkRequest_AutoVerified_WhenPhoneAndDobMatch()
    {
        // Arrange
        var parentUser = new User { Id = 20, PhoneNumber = "0901234567", FullName = "Phu Huynh A" };
        var guardian = new Guardian { Id = 1, UserId = 20 };
        var studentDob = new DateTime(2018, 5, 10);
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Em A",
            DateOfBirth = studentDob,
            StudentGuardians = new List<StudentGuardian>
            {
                new()
                {
                    Guardian = new Guardian { User = new User { PhoneNumber = "0901234567" } }
                }
            }
        };

        SetupRepository(new List<User> { parentUser });
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<StudentGuardian>());
        SetupRepository(new List<AdministrativeRequest>());
        SetupRepository(new List<GuardianLinkDetail>());

        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        var request = new SubmitChildLinkDto
        {
            StudentCode = "HS001",
            StudentDateOfBirth = studentDob,
            GuardianPhone = "0901234567",
            Relationship = RelationshipTypes.Mother
        };

        // Act
        var result = await _service.SubmitChildLinkRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.IsAutoVerified.Should().BeTrue();
        result.Data.Status.Should().Be("Approved");
        result.Code.Should().Be(SystemMessages.MSG13.Code);
    }

    [Fact]
    public async Task UTCID02_SubmitChildLinkRequest_AutoVerified_WhenIdCardAndDobMatch()
    {
        // Arrange
        var parentUser = new User { Id = 20, PhoneNumber = "0999999999", FullName = "Phu Huynh B" };
        var guardian = new Guardian { Id = 1, UserId = 20, IdentityCardNumber = "001200000001" };
        var studentDob = new DateTime(2018, 5, 10);
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Em B",
            DateOfBirth = studentDob,
            StudentGuardians = new List<StudentGuardian>
            {
                new()
                {
                    Guardian = new Guardian { IdentityCardNumber = "001200000001" }
                }
            }
        };

        SetupRepository(new List<User> { parentUser });
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<StudentGuardian>());
        SetupRepository(new List<AdministrativeRequest>());
        SetupRepository(new List<GuardianLinkDetail>());

        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        var request = new SubmitChildLinkDto
        {
            StudentCode = "HS001",
            StudentDateOfBirth = studentDob,
            GuardianIdentityCard = "001200000001",
            Relationship = RelationshipTypes.Father
        };

        // Act
        var result = await _service.SubmitChildLinkRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.IsAutoVerified.Should().BeTrue();
        result.Data.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task UTCID03_SubmitChildLinkRequest_ManualReview_WhenDobNotMatch()
    {
        // Arrange
        var parentUser = new User { Id = 20, PhoneNumber = "0901234567", FullName = "Phu Huynh C" };
        var guardian = new Guardian { Id = 1, UserId = 20 };
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Em C",
            DateOfBirth = new DateTime(2018, 5, 10),
            StudentGuardians = new List<StudentGuardian>()
        };

        SetupRepository(new List<User> { parentUser });
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<StudentGuardian>());
        SetupRepository(new List<AdministrativeRequest>());
        SetupRepository(new List<GuardianLinkDetail>());

        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        var request = new SubmitChildLinkDto
        {
            StudentCode = "HS001",
            StudentDateOfBirth = new DateTime(2018, 1, 1), // Mismatched DOB
            GuardianPhone = "0901234567"
        };

        // Act
        var result = await _service.SubmitChildLinkRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.IsAutoVerified.Should().BeFalse();
        result.Data.Status.Should().Be("UnderReview");
        result.Code.Should().Be(SystemMessages.MSG30.Code);
    }

    [Fact]
    public async Task UTCID04_SubmitChildLinkRequest_StudentNotFound_Fails()
    {
        // Arrange
        var parentUser = new User { Id = 20 };
        var guardian = new Guardian { Id = 1, UserId = 20 };

        SetupRepository(new List<User> { parentUser });
        SetupRepository(new List<Guardian> { guardian });

        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        var request = new SubmitChildLinkDto { StudentCode = "HS999" };

        // Act
        var result = await _service.SubmitChildLinkRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task UTCID05_SubmitChildLinkRequest_AlreadyLinked_ReturnsApproved()
    {
        // Arrange
        var parentUser = new User { Id = 20 };
        var guardian = new Guardian { Id = 1, UserId = 20 };
        var student = new Student { Id = 1, StudentCode = "HS001", FullName = "Em A" };

        SetupRepository(new List<User> { parentUser });
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } });

        StudentRepoMock.Setup(r => r.GetByStudentCodeAsync("HS001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        var request = new SubmitChildLinkDto { StudentCode = "HS001" };

        // Act
        var result = await _service.SubmitChildLinkRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.RequestNumber.Should().Be("ALREADY_LINKED");
        result.Code.Should().Be(SystemMessages.MSG13.Code);
    }

    [Fact]
    public async Task UTCID06_SubmitChildLinkRequest_MissingStudentCode_Fails()
    {
        // Arrange
        var request = new SubmitChildLinkDto { StudentCode = "" };

        // Act
        var result = await _service.SubmitChildLinkRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 29: GetLinkRequestsAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetLinkRequests_All_ReturnsList()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1,
                RequestType = "ChildLink",
                Status = RequestStatus.UnderReview,
                RequesterUser = new User { FullName = "Phu Huynh A" },
                Student = new Student { StudentCode = "HS001", FullName = "Em A" }
            }
        };
        SetupRepository(requests);

        // Act
        var result = await _service.GetLinkRequestsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID02_GetLinkRequests_FilterUnderReview_ReturnsFiltered()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1,
                RequestType = "ChildLink",
                Status = RequestStatus.UnderReview,
                RequesterUser = new User { FullName = "Phu Huynh A" },
                Student = new Student { StudentCode = "HS001" }
            },
            new()
            {
                Id = 2,
                RequestType = "ChildLink",
                Status = RequestStatus.Approved,
                RequesterUser = new User { FullName = "Phu Huynh B" },
                Student = new Student { StudentCode = "HS002" }
            }
        };
        SetupRepository(requests);

        // Act
        var result = await _service.GetLinkRequestsAsync("UnderReview");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].Status.Should().Be("UnderReview");
    }

    #endregion

    #region Method 30: VerifyChildLinkRequestAsync (UTCID01 - UTCID04)

    [Fact]
    public async Task UTCID01_VerifyChildLinkRequest_Approve_Success()
    {
        // Arrange
        var adminRequest = new AdministrativeRequest
        {
            Id = 1,
            RequestType = "ChildLink",
            Status = RequestStatus.UnderReview,
            RequesterUserId = 20,
            StudentId = 10,
            Student = new Student { Id = 10, FullName = "Em A" }
        };
        var guardian = new Guardian { Id = 1, UserId = 20 };
        var studentGuardians = new List<StudentGuardian>();

        SetupRepository(new List<AdministrativeRequest> { adminRequest });
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(studentGuardians);

        var review = new VerifyChildLinkDto
        {
            IsApproved = true,
            ReviewerNotes = "Giấy khai sinh hợp lệ",
            Relationship = RelationshipTypes.Mother,
            IsPrimaryContact = true
        };

        // Act
        var result = await _service.VerifyChildLinkRequestAsync(1, 10, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG13.Code);
        adminRequest.Status.Should().Be(RequestStatus.Approved);
        studentGuardians.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID02_VerifyChildLinkRequest_Reject_Success()
    {
        // Arrange
        var adminRequest = new AdministrativeRequest
        {
            Id = 1,
            RequestType = "ChildLink",
            Status = RequestStatus.UnderReview,
            RequesterUserId = 20,
            Student = new Student { FullName = "Em A" }
        };
        SetupRepository(new List<AdministrativeRequest> { adminRequest });

        var review = new VerifyChildLinkDto
        {
            IsApproved = false,
            ReviewerNotes = "Giấy khai sinh không rõ nét"
        };

        // Act
        var result = await _service.VerifyChildLinkRequestAsync(1, 10, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG32.Code);
        adminRequest.Status.Should().Be(RequestStatus.Rejected);
    }

    [Fact]
    public async Task UTCID03_VerifyChildLinkRequest_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());
        var review = new VerifyChildLinkDto { IsApproved = true };

        // Act
        var result = await _service.VerifyChildLinkRequestAsync(999, 10, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID04_VerifyChildLinkRequest_InvalidStatus_Fails()
    {
        // Arrange
        var adminRequest = new AdministrativeRequest
        {
            Id = 1,
            RequestType = "ChildLink",
            Status = RequestStatus.Approved // Already processed
        };
        SetupRepository(new List<AdministrativeRequest> { adminRequest });
        var review = new VerifyChildLinkDto { IsApproved = true };

        // Act
        var result = await _service.VerifyChildLinkRequestAsync(1, 10, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    #endregion

    #region Method 31: GetMyLinkedChildrenAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetMyLinkedChildren_HasChildren_ReturnsList()
    {
        // Arrange
        var guardian = new Guardian { Id = 1, UserId = 20 };
        SetupRepository(new List<Guardian> { guardian });

        var linkedStudents = new List<Student>
        {
            new()
            {
                Id = 1,
                StudentCode = "HS001",
                FullName = "Con A",
                StudentGuardians = new List<StudentGuardian> { new() { GuardianId = 1, Relationship = RelationshipTypes.Father } },
                ClassAssignments = new List<ClassAssignment> { new() { IsActive = true, Class = new Class { ClassName = "1A" } } }
            },
            new()
            {
                Id = 2,
                StudentCode = "HS002",
                FullName = "Con B",
                StudentGuardians = new List<StudentGuardian> { new() { GuardianId = 1, Relationship = RelationshipTypes.Father } },
                ClassAssignments = new List<ClassAssignment> { new() { IsActive = true, Class = new Class { ClassName = "3B" } } }
            }
        };

        StudentRepoMock.Setup(r => r.GetLinkedStudentsByParentUserIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(linkedStudents);

        // Act
        var result = await _service.GetMyLinkedChildrenAsync(20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID02_GetMyLinkedChildren_NoChildren_ReturnsEmpty()
    {
        // Arrange
        SetupRepository(new List<Guardian>());
        StudentRepoMock.Setup(r => r.GetLinkedStudentsByParentUserIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student>());

        // Act
        var result = await _service.GetMyLinkedChildrenAsync(20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    #endregion

    #region Method 32: GetChildDashboardAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetChildDashboard_LinkedParent_ReturnsDashboard()
    {
        // Arrange
        var guardian = new Guardian { Id = 1, UserId = 20 };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } });
        SetupRepository(new List<AttendanceRecord>());
        SetupRepository(new List<LunchRegistration>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<ClassSchedule>());

        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Em A",
            ClassAssignments = new List<ClassAssignment>
            {
                new()
                {
                    IsActive = true,
                    Class = new Class
                    {
                        Id = 10,
                        ClassName = "1A",
                        Grade = 1,
                        HomeroomTeacher = new StaffProfile
                        {
                            StaffCode = "GV001",
                            User = new User { FullName = "Co Mai", PhoneNumber = "0900000000" }
                        }
                    }
                }
            }
        };
        StudentRepoMock.Setup(r => r.GetStudentWithProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        var result = await _service.GetChildDashboardAsync(20, 1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.Student.FullName.Should().Be("Em A");
        result.Data.ClassInfo!.ClassName.Should().Be("1A");
        result.Data.HomeroomTeacher!.TeacherName.Should().Be("Co Mai");
    }

    [Fact]
    public async Task UTCID02_GetChildDashboard_NotLinked_Forbidden()
    {
        // Arrange
        var guardian = new Guardian { Id = 1, UserId = 20 };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<StudentGuardian>()); // No link to student 1

        // Act
        var result = await _service.GetChildDashboardAsync(20, 1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    #endregion
}
