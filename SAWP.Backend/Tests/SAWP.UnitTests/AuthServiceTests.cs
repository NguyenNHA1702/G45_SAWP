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
using Microsoft.Extensions.Configuration;
using MockQueryable;
using MockQueryable.Moq;
using Moq;
using Services.Implementations;
using Xunit;

namespace SAWP.UnitTests;

public class AuthServiceTests : TestBase
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["Jwt:Key"]).Returns("SAWP_SUPER_SECRET_SECURITY_KEY_2026_PRIMARY_SCHOOL_PLATFORM!");
        _configurationMock.Setup(c => c["Jwt:Issuer"]).Returns("SAWP.Backend");
        _configurationMock.Setup(c => c["Jwt:Audience"]).Returns("SAWP.Client");

        _authService = new AuthService(UnitOfWorkMock.Object, _configurationMock.Object);
    }

    [Fact]
    public async Task GetProfileAsync_WhenUserNotFound_ReturnsMSG07()
    {
        // Arrange
        var emptyUsers = new List<User>();
        UserRepoMock.Setup(r => r.Query()).Returns(() => emptyUsers.BuildMock());

        // Act
        var result = await _authService.GetProfileAsync(999);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG07.Code);
        result.Message.Should().Be(SystemMessages.MSG07.Content);
    }

    [Fact]
    public async Task GetProfileAsync_WhenUserExistsWithStaffAndGuardian_ReturnsPopulatedDto()
    {
        // Arrange
        var schoolClass = new Class
        {
            Id = 1,
            ClassName = "1A1",
            Grade = 1,
            Status = "Active"
        };

        var student = new Student
        {
            Id = 10,
            StudentCode = "HS001",
            FullName = "Nguyen Con",
            Gender = "Nam",
            DateOfBirth = new DateTime(2018, 5, 10),
            Status = "Active",
            ClassAssignments = new List<ClassAssignment>
            {
                new()
                {
                    Id = 100,
                    ClassId = 1,
                    Class = schoolClass,
                    IsActive = true
                }
            }
        };

        var user = new User
        {
            Id = 5,
            Username = "teacher_parent",
            FullName = "Nguyen Van A",
            Email = "a@school.edu.vn",
            PhoneNumber = "0901234567",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            UserRoles = new List<UserRole>
            {
                new() { Role = new Role { Id = 1, Name = "Teacher" } },
                new() { Role = new Role { Id = 2, Name = "Parent" } }
            },
            StaffProfile = new StaffProfile
            {
                Id = 2,
                StaffCode = "GV001",
                Department = "Tieu hoc",
                Position = "Giao vien",
                IsHomeroomTeacher = true,
                HomeroomClasses = new List<Class> { schoolClass }
            },
            Guardian = new Guardian
            {
                Id = 3,
                IdentityCardNumber = "0123456789",
                Occupation = "Kỹ sư",
                Address = "123 Nguyen Trai, Ha Noi",
                StudentGuardians = new List<StudentGuardian>
                {
                    new()
                    {
                        Student = student,
                        Relationship = RelationshipTypes.Father,
                        IsPrimaryContact = true
                    }
                }
            }
        };

        var users = new List<User> { user };
        UserRepoMock.Setup(r => r.Query()).Returns(() => users.BuildMock());

        // Act
        var result = await _authService.GetProfileAsync(5);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.UserId.Should().Be(5);
        result.Data.Username.Should().Be("teacher_parent");
        result.Data.FullName.Should().Be("Nguyen Van A");
        result.Data.Email.Should().Be("a@school.edu.vn");
        result.Data.PhoneNumber.Should().Be("0901234567");
        result.Data.Roles.Should().Contain(new[] { "Teacher", "Parent" });

        // Staff profile assertions
        result.Data.StaffProfile.Should().NotBeNull();
        result.Data.StaffProfile!.StaffCode.Should().Be("GV001");
        result.Data.StaffProfile.Department.Should().Be("Tieu hoc");
        result.Data.StaffProfile.Position.Should().Be("Giao vien");
        result.Data.StaffProfile.IsHomeroomTeacher.Should().BeTrue();
        result.Data.StaffProfile.HomeroomClassName.Should().Be("1A1");

        // Guardian profile assertions
        result.Data.GuardianProfile.Should().NotBeNull();
        result.Data.GuardianProfile!.IdentityCardNumber.Should().Be("0123456789");
        result.Data.GuardianProfile.Occupation.Should().Be("Kỹ sư");
        result.Data.GuardianProfile.Address.Should().Be("123 Nguyen Trai, Ha Noi");
        result.Data.GuardianProfile.Children.Should().HaveCount(1);

        var child = result.Data.GuardianProfile.Children[0];
        child.StudentId.Should().Be(10);
        child.StudentCode.Should().Be("HS001");
        child.FullName.Should().Be("Nguyen Con");
        child.ClassName.Should().Be("1A1");
        child.Relationship.Should().Be("Father");
        child.IsPrimaryContact.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenFullNameIsEmpty_ReturnsMSG01()
    {
        // Act
        var result = await _authService.UpdateProfileAsync(1, new UpdateProfileRequestDto
        {
            FullName = "   "
        });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
        result.Message.Should().Be(SystemMessages.MSG01.Content);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenUserNotFound_ReturnsMSG07()
    {
        // Arrange
        var emptyUsers = new List<User>();
        UserRepoMock.Setup(r => r.Query()).Returns(() => emptyUsers.BuildMock());

        // Act
        var result = await _authService.UpdateProfileAsync(999, new UpdateProfileRequestDto
        {
            FullName = "Nguyen Van B"
        });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG07.Code);
        result.Message.Should().Be(SystemMessages.MSG07.Content);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenValidGuardianUser_UpdatesFieldsAndReturnsMSG47()
    {
        // Arrange
        var user = new User
        {
            Id = 7,
            Username = "parent7",
            FullName = "Ten Cu",
            Email = "cu@school.edu.vn",
            PhoneNumber = "0900000000",
            IsActive = true,
            UserRoles = new List<UserRole>
            {
                new() { Role = new Role { Id = 3, Name = "Parent" } }
            },
            Guardian = new Guardian
            {
                Id = 12,
                IdentityCardNumber = "0987654321",
                Address = "Dia Chi Cu",
                Occupation = "Nghe Cu"
            }
        };

        var users = new List<User> { user };
        UserRepoMock.Setup(r => r.Query()).Returns(() => users.BuildMock());

        var updateRequest = new UpdateProfileRequestDto
        {
            FullName = "Nguyen Van Moi",
            PhoneNumber = "0911223344",
            Email = "moi@school.edu.vn",
            Address = "456 Kim Ma, Ha Noi",
            Occupation = "Bac si"
        };

        // Act
        var result = await _authService.UpdateProfileAsync(7, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG47.Code);
        result.Message.Should().Be(SystemMessages.MSG47.Content);

        user.FullName.Should().Be("Nguyen Van Moi");
        user.PhoneNumber.Should().Be("0911223344");
        user.Email.Should().Be("moi@school.edu.vn");
        user.UpdatedAt.Should().NotBeNull();
        user.Guardian!.Address.Should().Be("456 Kim Ma, Ha Noi");
        user.Guardian.Occupation.Should().Be("Bac si");

        result.Data.Should().NotBeNull();
        result.Data!.FullName.Should().Be("Nguyen Van Moi");
        result.Data.PhoneNumber.Should().Be("0911223344");
        result.Data.Email.Should().Be("moi@school.edu.vn");
        result.Data.GuardianProfile.Should().NotBeNull();
        result.Data.GuardianProfile!.Address.Should().Be("456 Kim Ma, Ha Noi");
        result.Data.GuardianProfile.Occupation.Should().Be("Bac si");

        UnitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenStaffUserWithoutGuardian_UpdatesUserCoreFieldsOnly()
    {
        // Arrange
        var user = new User
        {
            Id = 8,
            Username = "staff8",
            FullName = "Can Bo Cu",
            Email = "canbo@school.edu.vn",
            PhoneNumber = "0911111111",
            IsActive = true,
            UserRoles = new List<UserRole>
            {
                new() { Role = new Role { Id = 4, Name = "Admin" } }
            },
            Guardian = null
        };

        var users = new List<User> { user };
        UserRepoMock.Setup(r => r.Query()).Returns(() => users.BuildMock());

        var updateRequest = new UpdateProfileRequestDto
        {
            FullName = "Can Bo Moi",
            PhoneNumber = "0922222222",
            Email = "canbomoi@school.edu.vn",
            Address = "Ignored Address",
            Occupation = "Ignored Occupation"
        };

        // Act
        var result = await _authService.UpdateProfileAsync(8, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG47.Code);

        user.FullName.Should().Be("Can Bo Moi");
        user.PhoneNumber.Should().Be("0922222222");
        user.Email.Should().Be("canbomoi@school.edu.vn");
        user.Guardian.Should().BeNull();

        result.Data.Should().NotBeNull();
        result.Data!.FullName.Should().Be("Can Bo Moi");
        result.Data.PhoneNumber.Should().Be("0922222222");
        result.Data.GuardianProfile.Should().BeNull();

        UnitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
