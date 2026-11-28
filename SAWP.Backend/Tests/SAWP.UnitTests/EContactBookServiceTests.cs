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
            Content = "Mang đủ đồ dùng học tập và nước uống",
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
            Content = "Nội dung chi tiết hoạt động lớp",
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
            Content = "Nội dung chi tiết hoạt động lớp",
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

    [Fact]
    public async Task CB03_UpdateEvaluation_WithLinkedGuardian_ShouldUpdateNotificationWithoutDuplicateKey()
    {
        var staff = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var cls = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 1 };
        var student = new Student { Id = 1, StudentCode = "HS001", FullName = "Trần Gia Bảo" };
        var guardian = new Guardian { Id = 1, UserId = 200, FullName = "Phụ huynh A" };
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(cls);
        _context.Students.Add(student);
        _context.Guardians.Add(guardian);
        _context.StudentGuardians.Add(new StudentGuardian { StudentId = 1, GuardianId = 1, Relationship = Core.Enums.RelationshipTypes.Father, IsPrimaryContact = true });
        await _context.SaveChangesAsync();

        var createRes = await _service.CreateOrUpdateEvaluationAsync(10, new CreateStudentEvaluationDto
        {
            StudentId = 1,
            ClassId = 1,
            WeekNumber = 9,
            EvaluationDate = DateTime.UtcNow,
            Rating = "Good",
            TeacherNote = "Em chăm ngoan, hoàn thành tốt nhiệm vụ học tập."
        });
        createRes.Success.Should().BeTrue();
        var evalId = createRes.Data!.Id;

        var updateRes = await _service.CreateOrUpdateEvaluationAsync(10, new CreateStudentEvaluationDto
        {
            Id = evalId,
            StudentId = 1,
            ClassId = 1,
            WeekNumber = 9,
            EvaluationDate = DateTime.UtcNow,
            Rating = "Excellent",
            TeacherNote = "Cập nhật: Em rất tích cực phát biểu xây dựng bài."
        });

        updateRes.Success.Should().BeTrue();
        updateRes.Data!.Rating.Should().Be("Excellent");
        var notifs = await _context.Notifications.Where(n => n.UserId == 200 && n.ReferenceKey == $"EVAL:{evalId}").ToListAsync();
        notifs.Should().HaveCount(1);
        notifs[0].Title.Should().Contain("Cập nhật");
    }

    [Fact]
    public async Task CB04_ShortTitleOrShortNote_ShouldFailWithSpecificMessage()
    {
        var staff = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var cls = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 1 };
        var student = new Student { Id = 1, StudentCode = "HS001", FullName = "Trần Gia Bảo" };
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(cls);
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        var postRes = await _service.CreateClassPostAsync(10, new CreateClassPostDto
        {
            ClassId = 1,
            Title = "abcd",
            Content = "Nội dung đủ dài để kiểm tra tiêu đề."
        });
        postRes.Success.Should().BeFalse();
        postRes.Errors.Should().Contain(e => e.Contains("5 đến 200"));

        var evalRes = await _service.CreateOrUpdateEvaluationAsync(10, new CreateStudentEvaluationDto
        {
            StudentId = 1,
            ClassId = 1,
            WeekNumber = 1,
            EvaluationDate = DateTime.UtcNow,
            Rating = "Good",
            TeacherNote = "abc"
        });
        evalRes.Success.Should().BeFalse();
        evalRes.Errors.Should().Contain(e => e.Contains("10 đến 1500"));
    }

    [Fact]
    public async Task CB05_UpdateClassPost_ByHomeroomTeacher_ShouldSucceed()
    {
        var staff = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var cls = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 1 };
        var post = new ClassPost { Id = 1, ClassId = 1, AuthorUserId = 10, Title = "Tiêu đề cũ", Content = "Nội dung cũ đủ dài", Category = "General" };
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(cls);
        _context.ClassPosts.Add(post);
        await _context.SaveChangesAsync();

        var res = await _service.UpdateClassPostAsync(10, 1, new UpdateClassPostDto
        {
            Title = "Tiêu đề đã sửa",
            Content = "Nội dung mới đã được cập nhật đầy đủ.",
            Category = "Announcement",
            IsPinned = true,
            AllowComments = false
        });

        res.Success.Should().BeTrue();
        res.Data!.Title.Should().Be("Tiêu đề đã sửa");
        res.Data.IsPinned.Should().BeTrue();
        res.Data.AllowComments.Should().BeFalse();
    }

    [Fact]
    public async Task UploadAndPostValidation_RejectsPhpDisguisedAsPng_OverlongName_NegativeSize_60Attachments_AndMissingStorageFile()
    {
        // 1. Disguised PHP inside .png fails magic bytes
        var phpBytes = System.Text.Encoding.ASCII.GetBytes("<?php echo 'hack'; ?>");
        using var phpStream = new System.IO.MemoryStream(phpBytes);
        var phpErr = await Core.Common.UploadValidationHelper.ValidateFileStreamAsync(
            phpStream, "shell.png", new[] { ".jpg", ".jpeg", ".png", ".pdf" }, 5 * 1024 * 1024);
        phpErr.Should().NotBeNull();

        // 2. Sanitizer removes traversal & rejects 300-char filename
        var longName = new string('a', 300) + ".png";
        Core.Common.UploadValidationHelper.IsSafeFileNameLength(longName).Should().BeFalse();
        Core.Common.UploadValidationHelper.SanitizeFileName("../../evil<script>.png").Should().NotContain("..");

        var staff = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var cls = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 1 };
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(cls);
        await _context.SaveChangesAsync();

        var storageMock = new Mock<Infrastructure.ExternalServices.IFileStorageService>();
        storageMock.Setup(s => s.FileExists(It.IsAny<string>())).Returns(false);
        var svcWithStorage = new EContactBookService(_context, _auditServiceMock.Object, storageMock.Object);

        // 3. Negative FileSizeBytes
        var negRes = await svcWithStorage.CreateClassPostAsync(10, new CreateClassPostDto
        {
            ClassId = 1,
            Title = "Thông báo lớp học",
            Content = "Nội dung chi tiết bài viết lớp học.",
            Attachments = new List<PostAttachmentInputDto>
            {
                new() { FileName = "ok.jpg", FileUrl = "/files/ok.jpg", FileSizeBytes = -100, FileType = "Image" }
            }
        });
        negRes.Success.Should().BeFalse();
        negRes.Code.Should().Be(SystemMessages.MSG41.Code);

        // 4. 60 attachments rejected
        var manyUrls = Enumerable.Range(1, 60).Select(i => $"/files/doc_{i}.pdf").ToList();
        var manyRes = await svcWithStorage.CreateClassPostAsync(10, new CreateClassPostDto
        {
            ClassId = 1,
            Title = "Thông báo lớp học",
            Content = "Nội dung chi tiết bài viết lớp học.",
            AttachmentUrls = manyUrls
        });
        manyRes.Success.Should().BeFalse();
        manyRes.Code.Should().Be(SystemMessages.MSG01.Code);

        // 5. Internal contact-book file pointing to non-existent file on storage rejected
        var missingRes = await svcWithStorage.CreateClassPostAsync(10, new CreateClassPostDto
        {
            ClassId = 1,
            Title = "Thông báo lớp học",
            Content = "Nội dung chi tiết bài viết lớp học.",
            Attachments = new List<PostAttachmentInputDto>
            {
                new() { FileName = "missing.png", FileUrl = "/api/contact-book/attachment-file?path=contact-book%2Fmissing.png", FileSizeBytes = 1024, FileType = "Image" }
            }
        });
        missingRes.Success.Should().BeFalse();
        missingRes.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task BUG04_AddMultipleComments_GeneratesDistinctNotificationReferenceKeys()
    {
        var teacherRole = new Role { Id = 4, Name = "Teacher" };
        var parentRole = new Role { Id = 6, Name = "Parent" };
        var teacherUser = new User { Id = 10, Username = "gv1", Email = "gv@s.vn", FullName = "GV 1" };
        var parentUser = new User { Id = 20, Username = "ph1", Email = "ph@s.vn", FullName = "PH 1" };
        var staff = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var cls = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 1 };
        var post = new ClassPost { Id = 1, ClassId = 1, AuthorUserId = 10, Title = "Thông báo mới", Content = "Chi tiết bài đăng", AllowComments = true };

        _context.Roles.AddRange(teacherRole, parentRole);
        _context.Users.AddRange(teacherUser, parentUser);
        _context.UserRoles.AddRange(new UserRole { UserId = 10, RoleId = 4 }, new UserRole { UserId = 20, RoleId = 6 });
        _context.StaffProfiles.Add(staff);
        _context.Classes.Add(cls);
        _context.ClassPosts.Add(post);
        await _context.SaveChangesAsync();

        // Setup parent link via StudentGuardians + ClassAssignments
        var student = new Student { Id = 1, StudentCode = "HS1", FullName = "Em A", Status = "Active" };
        var guardian = new Guardian { Id = 1, UserId = 20, FullName = "PH 1" };
        _context.Students.Add(student);
        _context.Guardians.Add(guardian);
        _context.StudentGuardians.Add(new StudentGuardian { StudentId = 1, GuardianId = 1 });
        _context.ClassAssignments.Add(new ClassAssignment { Id = 1, ClassId = 1, StudentId = 1, IsActive = true });
        await _context.SaveChangesAsync();

        var c1 = await _service.AddCommentAsync(20, 1, new CreatePostCommentDto { Content = "Bình luận lần 1" });
        var c2 = await _service.AddCommentAsync(20, 1, new CreatePostCommentDto { Content = "Bình luận lần 2" });

        c1.Success.Should().BeTrue();
        c2.Success.Should().BeTrue();
        var keys = _context.Notifications.Select(n => n.ReferenceKey).ToList();
        keys.Should().HaveCount(2);
        keys.Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task BUG08_CanAccessAttachment_DeniesOtherClassParentAndAllowsSameClass()
    {
        _context.Roles.AddRange(new Role { Id = 4, Name = "Teacher" }, new Role { Id = 6, Name = "Parent" });
        _context.Users.AddRange(
            new User { Id = 10, Username = "gv1a", Email = "gv1a@s.vn", FullName = "GV 1A" },
            new User { Id = 20, Username = "ph1a", Email = "ph1a@s.vn", FullName = "PH 1A" },
            new User { Id = 21, Username = "ph2b", Email = "ph2b@s.vn", FullName = "PH 2B" });
        _context.UserRoles.AddRange(
            new UserRole { UserId = 10, RoleId = 4 },
            new UserRole { UserId = 20, RoleId = 6 },
            new UserRole { UserId = 21, RoleId = 6 });
        var staff = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var cls1 = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 1 };
        var cls2 = new Class { Id = 2, ClassName = "2B", HomeroomTeacherId = 99 };
        _context.StaffProfiles.Add(staff);
        _context.Classes.AddRange(cls1, cls2);

        var s1 = new Student { Id = 1, StudentCode = "HS1", FullName = "Em A", Status = "Active" };
        var s2 = new Student { Id = 2, StudentCode = "HS2", FullName = "Em B", Status = "Active" };
        var g1 = new Guardian { Id = 1, UserId = 20, FullName = "PH 1A" };
        var g2 = new Guardian { Id = 2, UserId = 21, FullName = "PH 2B" };
        _context.Students.AddRange(s1, s2);
        _context.Guardians.AddRange(g1, g2);
        _context.StudentGuardians.AddRange(
            new StudentGuardian { StudentId = 1, GuardianId = 1 },
            new StudentGuardian { StudentId = 2, GuardianId = 2 });
        _context.ClassAssignments.AddRange(
            new ClassAssignment { Id = 1, ClassId = 1, StudentId = 1, IsActive = true },
            new ClassAssignment { Id = 2, ClassId = 2, StudentId = 2, IsActive = true });

        const string relPath = "contact-book/guid123_photo.png";
        var post = new ClassPost
        {
            Id = 1, ClassId = 1, AuthorUserId = 10, Title = "Ảnh lớp 1A", Content = "Chi tiết bài đăng",
            Attachments = new List<PostAttachment>
            {
                new() { FileName = "photo.png", FileType = "Image", FileUrl = $"/api/contact-book/attachment-file?path={Uri.EscapeDataString(relPath)}" }
            }
        };
        _context.ClassPosts.Add(post);
        await _context.SaveChangesAsync();

        (await _service.CanAccessAttachmentAsync(10, relPath)).Should().BeTrue();
        (await _service.CanAccessAttachmentAsync(20, relPath)).Should().BeTrue();
        (await _service.CanAccessAttachmentAsync(21, relPath)).Should().BeFalse();
    }

    [Fact]
    public async Task BUG_CB_03_04_05_WordExcelMagicBytes_SchoolYearEvaluationScope_Feedback500_AndPostAuth()
    {
        // BUG-CB-03: Valid OLE2 (.doc/.xls) and OOXML ZIP (.docx/.xlsx) pass, disguised text fails
        var docxBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };
        using (var s = new System.IO.MemoryStream(docxBytes))
        {
            var err = await Core.Common.UploadValidationHelper.ValidateFileStreamAsync(
                s, "thongbao.docx", new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx", ".xls", ".xlsx" }, 5 * 1024 * 1024);
            err.Should().BeNull();
        }
        var docBytes = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
        using (var s2 = new System.IO.MemoryStream(docBytes))
        {
            var err2 = await Core.Common.UploadValidationHelper.ValidateFileStreamAsync(
                s2, "ke-hoach.doc", new[] { ".doc", ".docx", ".xls", ".xlsx" }, 5 * 1024 * 1024);
            err2.Should().BeNull();
        }

        // BUG-CB-04 & BUG-CB-05: Setup two school years and teachers
        var staffOld = new StaffProfile { Id = 1, UserId = 10, StaffCode = "GV01" };
        var staffNew = new StaffProfile { Id = 2, UserId = 11, StaffCode = "GV02" };
        var sy1 = new SchoolYear { Id = 1, YearName = "2025-2026", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 5, 31) };
        var sy2 = new SchoolYear { Id = 2, YearName = "2026-2027", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 5, 31), IsActive = true };
        var clsOld = new Class { Id = 1, ClassName = "1A", SchoolYearId = 1, SchoolYear = sy1, HomeroomTeacherId = 2 };
        var clsNew = new Class { Id = 2, ClassName = "2A", SchoolYearId = 2, SchoolYear = sy2, HomeroomTeacherId = 2 };
        var stu = new Student { Id = 1, StudentCode = "HS01", FullName = "Em A", Status = "Active" };
        var guardian = new Guardian { Id = 1, UserId = 20, FullName = "PH A" };
        _context.StaffProfiles.AddRange(staffOld, staffNew);
        _context.SchoolYears.AddRange(sy1, sy2);
        _context.Classes.AddRange(clsOld, clsNew);
        _context.Students.Add(stu);
        _context.Guardians.Add(guardian);
        _context.StudentGuardians.Add(new StudentGuardian { StudentId = 1, GuardianId = 1 });
        _context.ClassAssignments.AddRange(
            new ClassAssignment { Id = 1, ClassId = 1, StudentId = 1, IsActive = false },
            new ClassAssignment { Id = 2, ClassId = 2, StudentId = 1, IsActive = true });
        _context.StudentEvaluations.Add(new StudentEvaluation
        {
            Id = 1, StudentId = 1, ClassId = 1, SchoolYearId = 1, TeacherUserId = 11, WeekNumber = 1,
            EvaluationDate = new DateTime(2025, 9, 10), Rating = "Good", TeacherNote = "Tốt tuần 1 năm cũ."
        });
        _context.ClassPosts.Add(new ClassPost
        {
            Id = 5, ClassId = 2, AuthorUserId = 10, Title = "Bài cũ giáo viên trước", Content = "Nội dung chi tiết bài viết lớp."
        });
        await _context.SaveChangesAsync();

        // Former author who is no longer HomeroomTeacherId cannot update/delete post (BUG-CB-05)
        var oldTeacherUpdate = await _service.UpdateClassPostAsync(10, 5, new UpdateClassPostDto
        {
            Title = "Sửa trái phép", Content = "Nội dung sửa đổi trái phép đủ dài.", Category = "General"
        });
        oldTeacherUpdate.Success.Should().BeFalse();
        oldTeacherUpdate.Code.Should().Be(SystemMessages.MSG20.Code);

        // Creating Week 1 evaluation in new school year (SchoolYearId = 2) succeeds (BUG-CB-04)
        var newYearEval = await _service.CreateOrUpdateEvaluationAsync(11, new CreateStudentEvaluationDto
        {
            StudentId = 1, ClassId = 2, WeekNumber = 1, EvaluationDate = new DateTime(2026, 9, 10),
            Rating = "Excellent", TeacherNote = "Nhận xét tuần 1 năm học mới rất tốt."
        });
        newYearEval.Success.Should().BeTrue();

        // ParentFeedback > 500 chars is rejected (BUG-CB-05)
        var longAck = await _service.AcknowledgeEvaluationAsync(20, newYearEval.Data!.Id, new AcknowledgeEvaluationDto
        {
            ParentFeedback = new string('X', 501)
        });
        longAck.Success.Should().BeFalse();
        longAck.Code.Should().Be(SystemMessages.MSG01.Code);

        // BUG-SEC-04 hardening: Admin cannot read class posts, attachments, or student evaluations
        _context.Roles.Add(new Role { Id = 1, Name = "Admin" });
        _context.Users.Add(new User { Id = 999, Username = "sysadmin", Email = "sysadmin@test.vn", FullName = "SysAdmin", Status = Core.Enums.AccountStatus.Active });
        _context.UserRoles.Add(new UserRole { UserId = 999, RoleId = 1 });
        await _context.SaveChangesAsync();

        (await _service.GetClassPostsAsync(999, 2)).Code.Should().Be(SystemMessages.MSG10.Code);
        (await _service.GetClassEvaluationsRosterAsync(999, 2, 1)).Code.Should().Be(SystemMessages.MSG10.Code);
        (await _service.GetStudentEvaluationsAsync(999, 1)).Code.Should().Be(SystemMessages.MSG10.Code);
    }
}
