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
        _service = new GuardianService(UnitOfWorkMock.Object);
        SetupRepository(new List<Notification>());
    }

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
