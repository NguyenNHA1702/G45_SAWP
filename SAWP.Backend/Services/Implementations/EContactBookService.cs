using System.Text.Json;
using Core.Constants;
using Core.DTOs;
using Core.DTOs.ContactBook;
using Core.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class EContactBookService : IEContactBookService
{
    private readonly ApplicationDbContext _context;

    public EContactBookService(ApplicationDbContext context)
    {
        _context = context;
    }

    #region 1. Bảng tin lớp học (UC-61 & UC-62)

    public async Task<ApiResponse<List<ClassPostDto>>> GetClassPostsAsync(int currentUserId, int classId, CancellationToken cancellationToken = default)
    {
        var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<List<ClassPostDto>>.Fail(SystemMessages.MSG43);
        }

        // BR-56 & Data scope: Check if current user has access to this class
        bool isStaff = await _context.StaffProfiles.AnyAsync(sp => sp.UserId == currentUserId, cancellationToken);
        if (!isStaff)
        {
            // If parent: verify if parent has a linked student in this class
            bool isLinkedParent = await _context.StudentGuardians.AnyAsync(sg =>
                sg.Guardian.UserId == currentUserId &&
                _context.ClassAssignments.Any(ca => ca.StudentId == sg.StudentId && ca.ClassId == classId && ca.IsActive),
                cancellationToken);

            if (!isLinkedParent)
            {
                return ApiResponse<List<ClassPostDto>>.Fail(SystemMessages.MSG10);
            }
        }

        var posts = await _context.ClassPosts
            .AsNoTracking()
            .Include(cp => cp.AuthorUser)
            .Include(cp => cp.Class)
            .Include(cp => cp.Attachments)
            .Include(cp => cp.Comments).ThenInclude(c => c.User)
            .Include(cp => cp.Reactions)
            .Where(cp => cp.ClassId == classId)
            .OrderByDescending(cp => cp.IsPinned)
            .ThenByDescending(cp => cp.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = posts.Select(p =>
        {
            var userReaction = p.Reactions.FirstOrDefault(r => r.UserId == currentUserId);
            return new ClassPostDto
            {
                Id = p.Id,
                ClassId = p.ClassId,
                ClassName = p.Class.ClassName,
                AuthorUserId = p.AuthorUserId,
                AuthorName = p.AuthorUser?.FullName ?? "Giáo viên",
                Title = p.Title,
                Content = p.Content,
                Category = p.Category,
                IsPinned = p.IsPinned,
                AllowComments = p.AllowComments,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                ReactionCount = p.Reactions.Count,
                UserHasReacted = userReaction != null,
                UserReactionType = userReaction?.ReactionType,
                Attachments = p.Attachments.Select(a => new PostAttachmentDto
                {
                    Id = a.Id,
                    FileUrl = a.FileUrl,
                    FileName = a.FileName,
                    FileType = a.FileType,
                    FileSizeBytes = a.FileSizeBytes
                }).ToList(),
                Comments = p.Comments.OrderBy(c => c.CreatedAt).Select(c => new PostCommentDto
                {
                    Id = c.Id,
                    PostId = c.PostId,
                    UserId = c.UserId,
                    AuthorName = c.User?.FullName ?? "Người dùng",
                    AuthorRoleLabel = c.UserId == p.AuthorUserId ? "GVCN" : "Phụ huynh",
                    Content = c.Content,
                    CreatedAt = c.CreatedAt,
                    CanDelete = c.UserId == currentUserId || p.AuthorUserId == currentUserId
                }).ToList()
            };
        }).ToList();

        return ApiResponse<List<ClassPostDto>>.Ok(result);
    }

    public async Task<ApiResponse<ClassPostDto>> CreateClassPostAsync(int teacherUserId, CreateClassPostDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01);
        }

        // BR-57: Validate homeroom authority
        var homeroomClass = await _context.Classes.FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);
        if (homeroomClass == null)
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG43);
        }

        var staff = await _context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken);
        if (staff == null || homeroomClass.HomeroomTeacherId != staff.Id)
        {
            // Allow if admin or office staff, else restrict by BR-57
            bool isOfficeOrAdmin = await _context.UserRoles.AnyAsync(ur =>
                ur.UserId == teacherUserId && (ur.Role.Name == "OfficeStaff" || ur.Role.Name == "Admin" || ur.Role.Name == "SchoolBoard"), cancellationToken);
            if (!isOfficeOrAdmin)
            {
                return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG20);
            }
        }

        var post = new ClassPost
        {
            ClassId = request.ClassId,
            AuthorUserId = teacherUserId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category,
            IsPinned = request.IsPinned,
            AllowComments = request.AllowComments,
            CreatedAt = DateTime.UtcNow
        };

        if (request.AttachmentUrls != null && request.AttachmentUrls.Count > 0)
        {
            foreach (var url in request.AttachmentUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    var isImg = url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                url.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                url.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

                    post.Attachments.Add(new PostAttachment
                    {
                        FileUrl = url,
                        FileName = System.IO.Path.GetFileName(url),
                        FileType = isImg ? "Image" : "Document",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await _context.ClassPosts.AddAsync(post, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Bắn thông báo in-app cho phụ huynh của lớp
        var parentUserIds = await _context.StudentGuardians
            .Where(sg => _context.ClassAssignments.Any(ca => ca.StudentId == sg.StudentId && ca.ClassId == request.ClassId && ca.IsActive))
            .Select(sg => sg.Guardian.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var pUid in parentUserIds)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = pUid,
                Title = $"Thông báo mới từ lớp {homeroomClass.ClassName}",
                Content = $"GVCN vừa đăng bài: \"{post.Title}\"",
                Type = "ClassPost",
                ReferenceKey = $"POST:{post.Id}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }
        await _context.SaveChangesAsync(cancellationToken);

        var authorUser = await _context.Users.FindAsync(new object[] { teacherUserId }, cancellationToken);

        var dto = new ClassPostDto
        {
            Id = post.Id,
            ClassId = post.ClassId,
            ClassName = homeroomClass.ClassName,
            AuthorUserId = post.AuthorUserId,
            AuthorName = authorUser?.FullName ?? "Giáo viên",
            Title = post.Title,
            Content = post.Content,
            Category = post.Category,
            IsPinned = post.IsPinned,
            AllowComments = post.AllowComments,
            CreatedAt = post.CreatedAt,
            ReactionCount = 0,
            UserHasReacted = false,
            Attachments = post.Attachments.Select(a => new PostAttachmentDto
            {
                Id = a.Id,
                FileUrl = a.FileUrl,
                FileName = a.FileName,
                FileType = a.FileType
            }).ToList()
        };

        return ApiResponse<ClassPostDto>.Ok(dto, SystemMessages.MSG51);
    }

    public async Task<ApiResponse<bool>> TogglePinPostAsync(int teacherUserId, int postId, CancellationToken cancellationToken = default)
    {
        var post = await _context.ClassPosts.Include(p => p.Class).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG43);
        }

        var staff = await _context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken);
        if (staff == null || post.Class.HomeroomTeacherId != staff.Id)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG20);
        }

        post.IsPinned = !post.IsPinned;
        post.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> DeleteClassPostAsync(int teacherUserId, int postId, CancellationToken cancellationToken = default)
    {
        var post = await _context.ClassPosts.Include(p => p.Class).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG43);
        }

        var staff = await _context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken);
        if (staff == null || (post.Class.HomeroomTeacherId != staff.Id && post.AuthorUserId != teacherUserId))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG20);
        }

        _context.ClassPosts.Remove(post);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<PostCommentDto>> AddCommentAsync(int userId, int postId, CreatePostCommentDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return ApiResponse<PostCommentDto>.Fail(SystemMessages.MSG01);
        }

        var post = await _context.ClassPosts.Include(p => p.Class).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post == null)
        {
            return ApiResponse<PostCommentDto>.Fail(SystemMessages.MSG43);
        }

        // BR-58: Check if comments are allowed
        if (!post.AllowComments)
        {
            return ApiResponse<PostCommentDto>.Fail(SystemMessages.MSG54);
        }

        // Verify user belongs to class
        bool isStaff = await _context.StaffProfiles.AnyAsync(sp => sp.UserId == userId, cancellationToken);
        string roleLabel = isStaff ? "Giáo viên" : "Phụ huynh";

        if (!isStaff)
        {
            var linkedStudent = await _context.StudentGuardians
                .Where(sg => sg.Guardian.UserId == userId &&
                             _context.ClassAssignments.Any(ca => ca.StudentId == sg.StudentId && ca.ClassId == post.ClassId && ca.IsActive))
                .Select(sg => sg.Student.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            if (linkedStudent == null)
            {
                return ApiResponse<PostCommentDto>.Fail(SystemMessages.MSG10);
            }
            roleLabel = $"Phụ huynh em {linkedStudent}";
        }

        var comment = new PostComment
        {
            PostId = postId,
            UserId = userId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _context.PostComments.AddAsync(comment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);

        var dto = new PostCommentDto
        {
            Id = comment.Id,
            PostId = comment.PostId,
            UserId = comment.UserId,
            AuthorName = user?.FullName ?? "Người dùng",
            AuthorRoleLabel = roleLabel,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            CanDelete = true
        };

        return ApiResponse<PostCommentDto>.Ok(dto, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> DeleteCommentAsync(int currentUserId, int commentId, CancellationToken cancellationToken = default)
    {
        var comment = await _context.PostComments.Include(c => c.Post).ThenInclude(p => p.Class).FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG43);
        }

        var staff = await _context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == currentUserId, cancellationToken);
        bool isHomeroom = staff != null && comment.Post.Class.HomeroomTeacherId == staff.Id;
        bool isAuthor = comment.UserId == currentUserId;

        if (!isAuthor && !isHomeroom)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG10);
        }

        _context.PostComments.Remove(comment);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> ToggleReactionAsync(int userId, int postId, string reactionType, CancellationToken cancellationToken = default)
    {
        var post = await _context.ClassPosts.FindAsync(new object[] { postId }, cancellationToken);
        if (post == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG43);
        }

        var existing = await _context.PostReactions.FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, cancellationToken);
        if (existing != null)
        {
            _context.PostReactions.Remove(existing);
        }
        else
        {
            _context.PostReactions.Add(new PostReaction
            {
                PostId = postId,
                UserId = userId,
                ReactionType = string.IsNullOrWhiteSpace(reactionType) ? "Heart" : reactionType,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    #endregion

    #region 2. Sổ nhận xét học sinh & Ký nhận 2 chiều (UC-63 & UC-64)

    public async Task<ApiResponse<List<StudentEvaluationRosterItemDto>>> GetClassEvaluationsRosterAsync(int teacherUserId, int classId, int weekNumber, CancellationToken cancellationToken = default)
    {
        var targetClass = await _context.Classes.FindAsync(new object[] { classId }, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<List<StudentEvaluationRosterItemDto>>.Fail(SystemMessages.MSG43);
        }

        var activeStudents = await _context.ClassAssignments
            .AsNoTracking()
            .Include(ca => ca.Student)
            .Where(ca => ca.ClassId == classId && ca.IsActive)
            .Select(ca => ca.Student)
            .OrderBy(s => s.FullName)
            .ToListAsync(cancellationToken);

        var existingEvaluations = await _context.StudentEvaluations
            .AsNoTracking()
            .Include(se => se.TeacherUser)
            .Include(se => se.AcknowledgedByGuardian).ThenInclude(g => g!.User)
            .Where(se => se.ClassId == classId && se.WeekNumber == weekNumber)
            .ToListAsync(cancellationToken);

        var result = activeStudents.Select(s =>
        {
            var eval = existingEvaluations.FirstOrDefault(e => e.StudentId == s.Id);
            return new StudentEvaluationRosterItemDto
            {
                StudentId = s.Id,
                StudentName = s.FullName,
                StudentCode = s.StudentCode,
                Evaluation = eval == null ? null : MapToDto(eval, s.FullName, s.StudentCode, targetClass.ClassName)
            };
        }).ToList();

        return ApiResponse<List<StudentEvaluationRosterItemDto>>.Ok(result);
    }

    public async Task<ApiResponse<List<StudentEvaluationDto>>> GetStudentEvaluationsAsync(int currentUserId, int studentId, CancellationToken cancellationToken = default)
    {
        var student = await _context.Students.FindAsync(new object[] { studentId }, cancellationToken);
        if (student == null)
        {
            return ApiResponse<List<StudentEvaluationDto>>.Fail(SystemMessages.MSG11);
        }

        // BR-56: If parent, verify link
        bool isStaff = await _context.StaffProfiles.AnyAsync(sp => sp.UserId == currentUserId, cancellationToken);
        if (!isStaff)
        {
            bool isParent = await _context.StudentGuardians.AnyAsync(sg => sg.Guardian.UserId == currentUserId && sg.StudentId == studentId, cancellationToken);
            if (!isParent)
            {
                return ApiResponse<List<StudentEvaluationDto>>.Fail(SystemMessages.MSG10);
            }
        }

        var evaluations = await _context.StudentEvaluations
            .AsNoTracking()
            .Include(se => se.Class)
            .Include(se => se.TeacherUser)
            .Include(se => se.AcknowledgedByGuardian).ThenInclude(g => g!.User)
            .Where(se => se.StudentId == studentId)
            .OrderByDescending(se => se.WeekNumber)
            .ThenByDescending(se => se.EvaluationDate)
            .ToListAsync(cancellationToken);

        var result = evaluations.Select(e => MapToDto(e, student.FullName, student.StudentCode, e.Class.ClassName)).ToList();
        return ApiResponse<List<StudentEvaluationDto>>.Ok(result);
    }

    public async Task<ApiResponse<StudentEvaluationDto>> CreateOrUpdateEvaluationAsync(int teacherUserId, CreateStudentEvaluationDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TeacherNote))
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG01);
        }

        var targetClass = await _context.Classes.FindAsync(new object[] { request.ClassId }, cancellationToken);
        var student = await _context.Students.FindAsync(new object[] { request.StudentId }, cancellationToken);
        if (targetClass == null || student == null)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG11);
        }

        // BR-57: Check homeroom teacher
        var staff = await _context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken);
        if (staff == null || targetClass.HomeroomTeacherId != staff.Id)
        {
            bool isOfficeOrAdmin = await _context.UserRoles.AnyAsync(ur =>
                ur.UserId == teacherUserId && (ur.Role.Name == "OfficeStaff" || ur.Role.Name == "Admin"), cancellationToken);
            if (!isOfficeOrAdmin)
            {
                return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG20);
            }
        }

        // BR-61: Check existing evaluation for this week
        var existing = await _context.StudentEvaluations.FirstOrDefaultAsync(e =>
            e.StudentId == request.StudentId && e.WeekNumber == request.WeekNumber, cancellationToken);

        if (existing != null)
        {
            // BR-59: Lock if already acknowledged
            if (existing.IsAcknowledged)
            {
                return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG09);
            }

            existing.Rating = request.Rating;
            existing.BadgesJson = JsonSerializer.Serialize(request.Badges ?? new List<string>());
            existing.TeacherNote = request.TeacherNote.Trim();
            existing.PrivateRemind = request.PrivateRemind?.Trim();
            existing.EvaluationDate = request.EvaluationDate;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return ApiResponse<StudentEvaluationDto>.Ok(MapToDto(existing, student.FullName, student.StudentCode, targetClass.ClassName), SystemMessages.MSG52);
        }

        var newEval = new StudentEvaluation
        {
            StudentId = request.StudentId,
            ClassId = request.ClassId,
            TeacherUserId = teacherUserId,
            EvaluationDate = request.EvaluationDate,
            WeekNumber = request.WeekNumber,
            Rating = request.Rating,
            BadgesJson = JsonSerializer.Serialize(request.Badges ?? new List<string>()),
            TeacherNote = request.TeacherNote.Trim(),
            PrivateRemind = request.PrivateRemind?.Trim(),
            IsAcknowledged = false,
            CreatedAt = DateTime.UtcNow
        };

        await _context.StudentEvaluations.AddAsync(newEval, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Notify parent
        var guardianUserIds = await _context.StudentGuardians
            .Where(sg => sg.StudentId == request.StudentId)
            .Select(sg => sg.Guardian.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var gUid in guardianUserIds)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = gUid,
                Title = $"Phiếu nhận xét tuần {request.WeekNumber} của em {student.FullName}",
                Content = $"GVCN đã gửi nhận xét nề nếp tuần {request.WeekNumber}. Vui lòng xem và ký xác nhận.",
                Type = "StudentEvaluation",
                ReferenceKey = $"EVAL:{newEval.Id}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<StudentEvaluationDto>.Ok(MapToDto(newEval, student.FullName, student.StudentCode, targetClass.ClassName), SystemMessages.MSG52);
    }

    public async Task<ApiResponse<StudentEvaluationDto>> AcknowledgeEvaluationAsync(int guardianUserId, int evaluationId, AcknowledgeEvaluationDto request, CancellationToken cancellationToken = default)
    {
        var eval = await _context.StudentEvaluations
            .Include(e => e.Student)
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == evaluationId, cancellationToken);

        if (eval == null)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG43);
        }

        // BR-56: Verify guardian owns student
        var guardian = await _context.Guardians.FirstOrDefaultAsync(g => g.UserId == guardianUserId, cancellationToken);
        if (guardian == null)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG10);
        }

        bool isLinked = await _context.StudentGuardians.AnyAsync(sg => sg.GuardianId == guardian.Id && sg.StudentId == eval.StudentId, cancellationToken);
        if (!isLinked)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG10);
        }

        if (eval.IsAcknowledged)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG09);
        }

        eval.IsAcknowledged = true;
        eval.AcknowledgedAt = DateTime.UtcNow;
        eval.AcknowledgedByGuardianId = guardian.Id;
        eval.ParentFeedback = request.ParentFeedback?.Trim();
        eval.UpdatedAt = DateTime.UtcNow;

        // Thông báo lại cho GVCN
        _context.Notifications.Add(new Notification
        {
            UserId = eval.TeacherUserId,
            Title = $"Phụ huynh đã ký nhận phiếu nhận xét",
            Content = $"Phụ huynh em {eval.Student.FullName} đã xác nhận phiếu tuần {eval.WeekNumber}." +
                      (!string.IsNullOrWhiteSpace(eval.ParentFeedback) ? $" Phản hồi: \"{eval.ParentFeedback}\"" : ""),
            Type = "EvaluationAcknowledged",
            ReferenceKey = $"EVAL:{eval.Id}",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _context.SaveChangesAsync(cancellationToken);

        var guardianUser = await _context.Users.FindAsync(new object[] { guardianUserId }, cancellationToken);
        var dto = MapToDto(eval, eval.Student.FullName, eval.Student.StudentCode, eval.Class.ClassName);
        dto.AcknowledgedByGuardianName = guardianUser?.FullName;

        return ApiResponse<StudentEvaluationDto>.Ok(dto, SystemMessages.MSG53);
    }

    #endregion

    private static StudentEvaluationDto MapToDto(StudentEvaluation e, string studentName, string studentCode, string className)
    {
        List<string> badges = new();
        try
        {
            if (!string.IsNullOrWhiteSpace(e.BadgesJson))
            {
                badges = JsonSerializer.Deserialize<List<string>>(e.BadgesJson) ?? new();
            }
        }
        catch { }

        return new StudentEvaluationDto
        {
            Id = e.Id,
            StudentId = e.StudentId,
            StudentName = studentName,
            StudentCode = studentCode,
            ClassId = e.ClassId,
            ClassName = className,
            TeacherUserId = e.TeacherUserId,
            TeacherName = e.TeacherUser?.FullName ?? "Giáo viên",
            EvaluationDate = e.EvaluationDate,
            WeekNumber = e.WeekNumber,
            Rating = e.Rating,
            Badges = badges,
            TeacherNote = e.TeacherNote,
            PrivateRemind = e.PrivateRemind,
            IsAcknowledged = e.IsAcknowledged,
            AcknowledgedAt = e.AcknowledgedAt,
            AcknowledgedByGuardianName = e.AcknowledgedByGuardian?.User?.FullName,
            ParentFeedback = e.ParentFeedback,
            CreatedAt = e.CreatedAt
        };
    }
}
