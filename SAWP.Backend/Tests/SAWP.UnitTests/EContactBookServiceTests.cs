using System;
using System.Collections.Generic;
using System.Threading;
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
            "DELETE_COMMENT",
            "PostComment",
            "100",
            "Bình luận không phù hợp",
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
}
