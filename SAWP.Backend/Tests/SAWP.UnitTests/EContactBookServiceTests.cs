using System;
using System.Collections.Generic;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;
using Core.Constants;
using Core.DTOs.ContactBook;
using Core.Entities;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Services.Implementations;
using Services.Interfaces;
using Xunit;

namespace SAWP.UnitTests;

public class EContactBookServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly EContactBookService _service;

    // Decodes one property of the audit values exactly as AuditService stores them.
    private static string? AuditValue(object values, string property)
    {
        using var doc = JsonDocument.Parse(AuditService.SerializeValues(values)!);
        return doc.RootElement.GetProperty(property).GetString();
    }

    public EContactBookServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _auditServiceMock = new Mock<IAuditService>();
        _service = new EContactBookService(_context, _auditServiceMock.Object);
    }

    [Fact]
    public async Task TC_CTB_04_PublishPost_WithAttachmentUnder5MB_ShouldSucceed()
    {
        // Arrange
        var teacherUserId = 10;
        var staff = new StaffProfile { Id = 1, UserId = teacherUserId, StaffCode = "GV001" };
        var homeroomClass = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = staff.Id };
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(homeroomClass);
        await _context.SaveChangesAsync();

        var request = new CreateClassPostDto
        {
            ClassId = 1,
            Title = "hoạt động ngoại khóa",
            Content = "Mang đủ đồ",
            Attachments = new List<PostAttachmentInputDto>
            {
                new() { FileName = "ngoai_khoa.jpg", FileUrl = "/files/ngoai_khoa.jpg", FileSizeBytes = 5 * 1024 * 1024, FileType = "Image" }
            }
        };

        // Act
        var result = await _service.CreateClassPostAsync(teacherUserId, request);

        // Assert
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG51.Code);
    }

    [Fact]
    public async Task TC_CTB_05_PublishPost_WithAttachmentExceeding5MB_ShouldFail_MSG41()
    {
        // Arrange
        var teacherUserId = 10;
        var staff = new StaffProfile { Id = 1, UserId = teacherUserId, StaffCode = "GV001" };
        var homeroomClass = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = staff.Id };
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(homeroomClass);
        await _context.SaveChangesAsync();

        var request = new CreateClassPostDto
        {
            ClassId = 1,
            Title = "Video hoạt động",
            Content = "Nội dung",
            Attachments = new List<PostAttachmentInputDto>
            {
                new() { FileName = "video_hoat_dong.jpg", FileUrl = "/files/video_hoat_dong.jpg", FileSizeBytes = 6 * 1024 * 1024, FileType = "Image" }
            }
        };

        // Act
        var result = await _service.CreateClassPostAsync(teacherUserId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG41.Code);
    }

    [Fact]
    public async Task TC_CTB_06_PublishPost_WithUnsupportedExtension_ShouldFail_MSG41()
    {
        // Arrange
        var teacherUserId = 10;
        var staff = new StaffProfile { Id = 1, UserId = teacherUserId, StaffCode = "GV001" };
        var homeroomClass = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = staff.Id };
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(homeroomClass);
        await _context.SaveChangesAsync();

        var request = new CreateClassPostDto
        {
            ClassId = 1,
            Title = "Phần mềm",
            Content = "Nội dung",
            Attachments = new List<PostAttachmentInputDto>
            {
                new() { FileName = "setup.exe", FileUrl = "/files/setup.exe", FileSizeBytes = 1024, FileType = "Document" }
            }
        };

        // Act
        var result = await _service.CreateClassPostAsync(teacherUserId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG41.Code);
    }

    [Fact]
    public async Task TC_CTB_14_DeleteComment_ShouldLogAuditAction()
    {
        // Arrange
        var teacherUserId = 10;
        var staff = new StaffProfile { Id = 1, UserId = teacherUserId, StaffCode = "GV001" };
        var homeroomClass = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = staff.Id };
        var post = new ClassPost { Id = 1, ClassId = 1, AuthorUserId = teacherUserId, Title = "Bài viết", Content = "Chi tiết", AllowComments = true };
        var comment = new PostComment { Id = 100, PostId = 1, UserId = 20, Content = "Bình luận không phù hợp" };

        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(homeroomClass);
        _context.ClassPosts.Add(post);
        _context.PostComments.Add(comment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteCommentAsync(teacherUserId, 100);

        // Assert
        result.Success.Should().BeTrue();
        _auditServiceMock.Verify(a => a.LogActionAsync(
            teacherUserId,
            AuditActions.PostCommentDelete,
            nameof(PostComment),
            "100",
            It.Is<object>(o => AuditValue(o, "Content") == "Bình luận không phù hợp"),
            null,
            null,
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task TC_CTB_16_SubmitEvaluation_WithoutRating_ShouldFail_MSG01()
    {
        // Arrange
        var teacherUserId = 10;
        var staff = new StaffProfile { Id = 1, UserId = teacherUserId, StaffCode = "GV001" };
        var homeroomClass = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = staff.Id };
        var student = new Student { Id = 2, FullName = "Trần Gia Bảo", StudentCode = "HS002" };

        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(homeroomClass);
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        var request = new CreateStudentEvaluationDto
        {
            StudentId = 2,
            ClassId = 1,
            WeekNumber = 10,
            Rating = "", // Empty rating
            TeacherNote = "Con ngoan và chăm chỉ"
        };

        // Act
        var result = await _service.CreateOrUpdateEvaluationAsync(teacherUserId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task TC_CTB_17_SubmitSecondEvaluation_ForSameStudentAndWeek_ShouldReject_BR61()
    {
        // Arrange
        var teacherUserId = 10;
        var staff = new StaffProfile { Id = 1, UserId = teacherUserId, StaffCode = "GV001" };
        var homeroomClass = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = staff.Id };
        var student = new Student { Id = 1, FullName = "Trần Gia Bảo", StudentCode = "HS001" };
        var existingEval = new StudentEvaluation
        {
            Id = 50,
            StudentId = 1,
            ClassId = 1,
            TeacherUserId = teacherUserId,
            WeekNumber = 10,
            Rating = "Tốt",
            TeacherNote = "Tiến bộ rõ trong giờ học",
            IsAcknowledged = false
        };

        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(homeroomClass);
        _context.Students.Add(student);
        _context.StudentEvaluations.Add(existingEval);
        await _context.SaveChangesAsync();

        // Submitting duplicate evaluation without matching Id
        var request = new CreateStudentEvaluationDto
        {
            Id = null, // Not editing existing Id
            StudentId = 1,
            ClassId = 1,
            WeekNumber = 10,
            Rating = "Khá",
            TeacherNote = "Nhận xét mới"
        };

        // Act
        var result = await _service.CreateOrUpdateEvaluationAsync(teacherUserId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("BR-61");

        // Verify existing evaluation unchanged
        var current = await _context.StudentEvaluations.FindAsync(50);
        current!.Rating.Should().Be("Tốt");
    }

    [Fact]
    public async Task TC_CTB_22_TeacherEdits_AcknowledgedEvaluation_ShouldFail_MSG09()
    {
        // Arrange
        var teacherUserId = 10;
        var staff = new StaffProfile { Id = 1, UserId = teacherUserId, StaffCode = "GV001" };
        var homeroomClass = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = staff.Id };
        var student = new Student { Id = 1, FullName = "Trần Gia Bảo", StudentCode = "HS001" };
        var existingEval = new StudentEvaluation
        {
            Id = 50,
            StudentId = 1,
            ClassId = 1,
            TeacherUserId = teacherUserId,
            WeekNumber = 10,
            Rating = "Tốt",
            TeacherNote = "Tiến bộ",
            IsAcknowledged = true, // Already acknowledged!
            AcknowledgedAt = DateTime.UtcNow
        };

        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(homeroomClass);
        _context.Students.Add(student);
        _context.StudentEvaluations.Add(existingEval);
        await _context.SaveChangesAsync();

        var request = new CreateStudentEvaluationDto
        {
            Id = 50,
            StudentId = 1,
            ClassId = 1,
            WeekNumber = 10,
            Rating = "Khá",
            TeacherNote = "Cố gắng sửa rating"
        };

        // Act
        var result = await _service.CreateOrUpdateEvaluationAsync(teacherUserId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task GetClassEvaluationsRoster_ByUnauthorizedRole_ShouldFail_MSG10()
    {
        var kitchenRole = new Role { Id = 5, Name = "ServiceStaff" };
        var kitchenUser = new User { Id = 50, Username = "kitchen1", FullName = "Kitchen" };
        var cls = new Class { Id = 1, ClassName = "2A", HomeroomTeacherId = 99 };
        _context.Roles.Add(kitchenRole);
        _context.Users.Add(kitchenUser);
        _context.UserRoles.Add(new UserRole { UserId = 50, RoleId = 5 });
        _context.Classes.Add(cls);
        await _context.SaveChangesAsync();

        var result = await _service.GetClassEvaluationsRosterAsync(50, 1, 10);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task GetStudentEvaluations_ByParent_ShouldIncludePrivateRemind()
    {
        var parentRole = new Role { Id = 6, Name = "Parent" };
        var teacherUser = new User { Id = 10, Username = "gv1", FullName = "GV 1" };
        var parentUser = new User { Id = 60, Username = "parent1", FullName = "Parent 1" };
        var guardian = new Guardian { Id = 10, UserId = 60 };
        var student = new Student { Id = 1, StudentCode = "HS001", FullName = "Bé An" };
        var cls = new Class { Id = 1, ClassName = "2A" };
        var eval = new StudentEvaluation
        {
            Id = 1,
            StudentId = 1,
            ClassId = 1,
            TeacherUserId = 10,
            WeekNumber = 5,
            Rating = "Good",
            TeacherNote = "Chăm ngoan",
            PrivateRemind = "Dặn dò riêng cho phụ huynh",
            EvaluationDate = DateTime.UtcNow
        };
        _context.Roles.Add(parentRole);
        _context.Users.AddRange(teacherUser, parentUser);
        _context.UserRoles.Add(new UserRole { UserId = 60, RoleId = 6 });
        _context.Guardians.Add(guardian);
        _context.Students.Add(student);
        _context.StudentGuardians.Add(new StudentGuardian { GuardianId = 10, StudentId = 1, Relationship = Core.Enums.RelationshipTypes.Mother });
        _context.Classes.Add(cls);
        _context.StudentEvaluations.Add(eval);
        await _context.SaveChangesAsync();

        var result = await _service.GetStudentEvaluationsAsync(60, 1);

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].PrivateRemind.Should().Be("Dặn dò riêng cho phụ huynh");
    }

    [Fact]
    public async Task CreateOrUpdateEvaluation_ExceedingMaxLengths_ShouldFail()
    {
        var teacher = new User { Id = 10, Username = "gv1", FullName = "GV 1" };
        var staff = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var cls = new Class { Id = 1, ClassName = "2A", HomeroomTeacherId = 1 };
        var student = new Student { Id = 1, StudentCode = "HS001", FullName = "Bé An" };
        _context.Users.Add(teacher);
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(cls);
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        var tooLongRemind = new CreateStudentEvaluationDto
        {
            StudentId = 1,
            ClassId = 1,
            WeekNumber = 5,
            Rating = "Good",
            TeacherNote = "Chăm ngoan",
            PrivateRemind = new string('A', 501)
        };
        var remindRes = await _service.CreateOrUpdateEvaluationAsync(10, tooLongRemind);
        remindRes.Success.Should().BeFalse();

        var tooLongNote = new CreateStudentEvaluationDto
        {
            StudentId = 1,
            ClassId = 1,
            WeekNumber = 5,
            Rating = "Good",
            TeacherNote = new string('B', 1501)
        };
        var noteRes = await _service.CreateOrUpdateEvaluationAsync(10, tooLongNote);
        noteRes.Success.Should().BeFalse();
    }

    [Fact]
    public async Task AddComment_LongContent_ShouldSucceedAndTruncateNotification()
    {
        var staff = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var cls = new Class { Id = 1, ClassName = "2A", HomeroomTeacherId = 1 };
        var parentUser = new User { Id = 60, Username = "parent1", FullName = "Phụ huynh 1" };
        var guardian = new Guardian { Id = 10, UserId = 60 };
        var student = new Student { Id = 1, StudentCode = "HS001", FullName = "Bé An" };
        var post = new ClassPost { Id = 1, ClassId = 1, AuthorUserId = 10, Title = new string('T', 120), Content = "Nội dung", AllowComments = true };

        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(cls);
        _context.Users.Add(parentUser);
        _context.Guardians.Add(guardian);
        _context.Students.Add(student);
        _context.StudentGuardians.Add(new StudentGuardian { GuardianId = 10, StudentId = 1, Relationship = Core.Enums.RelationshipTypes.Mother });
        _context.ClassAssignments.Add(new ClassAssignment { Id = 1, ClassId = 1, StudentId = 1, IsActive = true });
        _context.ClassPosts.Add(post);
        await _context.SaveChangesAsync();

        var longComment = new string('A', 600);
        var result = await _service.AddCommentAsync(60, 1, new CreatePostCommentDto { Content = longComment });

        result.Success.Should().BeTrue();
        result.Data!.Content.Length.Should().Be(600);
    }

    [Fact]
    public async Task CreateOrUpdateEvaluation_InvalidRating_ShouldFail_MSG01()
    {
        var staff = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var cls = new Class { Id = 1, ClassName = "2A", HomeroomTeacherId = 1 };
        var student = new Student { Id = 1, StudentCode = "HS001", FullName = "Bé An" };
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(cls);
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        var result = await _service.CreateOrUpdateEvaluationAsync(10, new CreateStudentEvaluationDto
        {
            StudentId = 1,
            ClassId = 1,
            WeekNumber = 5,
            Rating = "<b>Xuat sac 10 diem</b>",
            TeacherNote = "Ghi chú"
        });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task ToggleCommentsLock_ShouldBlockSubsequentComments_MSG54()
    {
        var staff = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var cls = new Class { Id = 1, ClassName = "2A", HomeroomTeacherId = 1 };
        var post = new ClassPost { Id = 1, ClassId = 1, AuthorUserId = 10, Title = "Thông báo", Content = "Nội dung", AllowComments = true };
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(cls);
        _context.ClassPosts.Add(post);
        await _context.SaveChangesAsync();

        var lockRes = await _service.ToggleCommentsLockAsync(10, 1);
        lockRes.Success.Should().BeTrue();
        lockRes.Data.Should().BeFalse();

        var commentRes = await _service.AddCommentAsync(10, 1, new CreatePostCommentDto { Content = "Bình luận sau khi khóa" });
        commentRes.Success.Should().BeFalse();
        commentRes.Code.Should().Be(SystemMessages.MSG54.Code);
    }
}
