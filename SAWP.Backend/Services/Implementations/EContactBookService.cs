using System.Text.Json;
using Core.Common;
using Core.Constants;
using Core.DTOs;
using Core.DTOs.ContactBook;
using Core.Entities;
using Core.Enums;
using Infrastructure.Data;
using Infrastructure.ExternalServices;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class EContactBookService : IEContactBookService
{
    private const int MaxAttachmentsPerPost = 10;
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IFileStorageService? _fileStorageService;

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "General", "Homework", "Activity", "Announcement"
    };

    private static readonly HashSet<string> AllowedRatings = new(StringComparer.OrdinalIgnoreCase)
    {
        "Excellent", "Good", "NeedsAttention",
        "Xuất sắc", "Tốt", "Khá", "Cần cố gắng"
    };

    private static readonly HashSet<string> AllowedReactions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Like", "Heart", "Clap", "Smile"
    };

    public EContactBookService(
        ApplicationDbContext context,
        IAuditService auditService,
        IFileStorageService? fileStorageService = null)
    {
        _context = context;
        _auditService = auditService;
        _fileStorageService = fileStorageService;
    }

    private static string? TryExtractInternalContactBookPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        const string prefix1 = "/api/contact-book/attachment-file?path=";
        const string prefix2 = "/api/contactbook/attachment-file?path=";

        string? encoded = null;
        if (trimmed.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase))
        {
            encoded = trimmed[prefix1.Length..];
        }
        else if (trimmed.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase))
        {
            encoded = trimmed[prefix2.Length..];
        }

        if (encoded == null) return null;

        var qExtra = encoded.IndexOf('&');
        if (qExtra >= 0) encoded = encoded[..qExtra];

        var decoded = Uri.UnescapeDataString(encoded).Replace('\\', '/').Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(decoded)
            || decoded.Contains("..", StringComparison.Ordinal)
            || decoded.Contains(':', StringComparison.Ordinal)
            || Path.IsPathRooted(decoded)
            || !decoded.StartsWith("contact-book/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return decoded;
    }

    private static bool IsSafeInternalAttachmentUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        if (trimmed.Contains("..", StringComparison.Ordinal)) return false;
        if (trimmed.StartsWith("/api/contact-book/attachment-file?path=", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("/api/contactbook/attachment-file?path=", StringComparison.OrdinalIgnoreCase))
        {
            return TryExtractInternalContactBookPath(trimmed) != null;
        }

        return trimmed.StartsWith("/files/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeExternalOrInternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        if (IsSafeInternalAttachmentUrl(trimmed)) return true;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
        return value[..maxLength] + "...";
    }

    #region 1. Bảng tin lớp học (UC-61 & UC-62)

    private async Task<bool> HasClassAccessAsync(
        int currentUserId,
        int classId,
        int? homeroomTeacherId,
        CancellationToken cancellationToken)
    {
        var roleNames = await _context.UserRoles
            .Where(ur => ur.UserId == currentUserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken);

        if (roleNames.Contains("OfficeStaff") || roleNames.Contains("SchoolBoard"))
        {
            return true;
        }

        var staff = await _context.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == currentUserId, cancellationToken);
        if (staff != null && homeroomTeacherId == staff.Id)
        {
            return true;
        }

        return await _context.StudentGuardians.AnyAsync(sg =>
            sg.Guardian.UserId == currentUserId &&
            _context.ClassAssignments.Any(ca => ca.StudentId == sg.StudentId && ca.ClassId == classId && ca.IsActive),
            cancellationToken);
    }

    public async Task<bool> CanAccessAttachmentAsync(int currentUserId, string cleanRelativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cleanRelativePath)) return false;

        var roleNames = await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == currentUserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken);

        if (roleNames.Contains("OfficeStaff") || roleNames.Contains("SchoolBoard"))
        {
            return true;
        }

        var leafFileName = Path.GetFileName(cleanRelativePath);
        if (string.IsNullOrWhiteSpace(leafFileName)) return false;
        var escapedLeaf = Uri.EscapeDataString(leafFileName);

        var candidateAttachments = await _context.PostAttachments
            .AsNoTracking()
            .Include(pa => pa.Post).ThenInclude(p => p.Class)
            .Where(pa => pa.FileUrl.Contains(leafFileName) || pa.FileUrl.Contains(escapedLeaf))
            .ToListAsync(cancellationToken);

        var matched = candidateAttachments
            .Where(pa => string.Equals(TryExtractInternalContactBookPath(pa.FileUrl), cleanRelativePath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matched.Count == 0)
        {
            // Draft upload preview before posting: Teachers may preview; Parents cannot probe unattached files
            return roleNames.Contains("Teacher");
        }

        foreach (var att in matched)
        {
            if (att.Post?.Class != null && await HasClassAccessAsync(currentUserId, att.Post.ClassId, att.Post.Class.HomeroomTeacherId, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<ApiResponse<List<ClassPostDto>>> GetClassPostsAsync(int currentUserId, int classId, CancellationToken cancellationToken = default)
    {
        var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<List<ClassPostDto>>.Fail(SystemMessages.MSG43);
        }

        // BR-56 & Data scope: Check if current user has access to this class
        if (!await HasClassAccessAsync(currentUserId, classId, targetClass.HomeroomTeacherId, cancellationToken))
        {
            return ApiResponse<List<ClassPostDto>>.Fail(SystemMessages.MSG10);
        }

        var linkedGuardians = await _context.StudentGuardians
            .AsNoTracking()
            .Where(sg => _context.ClassAssignments.Any(ca => ca.StudentId == sg.StudentId && ca.ClassId == classId && ca.IsActive))
            .Where(sg => sg.Guardian.UserId != null)
            .Select(sg => new { UserId = sg.Guardian.UserId!.Value, StudentName = sg.Student.FullName })
            .ToListAsync(cancellationToken);

        var guardianStudentMap = linkedGuardians
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().StudentName);

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
                CreatedAt = UtcDateTimeHelper.Normalize(p.CreatedAt),
                UpdatedAt = UtcDateTimeHelper.Normalize(p.UpdatedAt),
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
                Comments = p.Comments.OrderBy(c => c.CreatedAt).Select(c =>
                {
                    string roleLabel = "Phụ huynh";
                    if (c.UserId == p.AuthorUserId)
                    {
                        roleLabel = "GVCN";
                    }
                    else if (guardianStudentMap.TryGetValue(c.UserId, out var childName))
                    {
                        roleLabel = $"Phụ huynh em {childName}";
                    }

                    return new PostCommentDto
                    {
                        Id = c.Id,
                        PostId = c.PostId,
                        UserId = c.UserId,
                        AuthorName = c.User?.FullName ?? "Người dùng",
                        AuthorRoleLabel = roleLabel,
                        Content = c.Content,
                        CreatedAt = UtcDateTimeHelper.Normalize(c.CreatedAt),
                        CanDelete = c.UserId == currentUserId || p.AuthorUserId == currentUserId
                    };
                }).ToList()
            };
        }).ToList();

        return ApiResponse<List<ClassPostDto>>.Ok(result);
    }

    public async Task<ApiResponse<ClassPostDto>> CreateClassPostAsync(int teacherUserId, CreateClassPostDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Tiêu đề và nội dung bài viết không được để trống." });
        }

        var trimmedTitle = request.Title.Trim();
        var trimmedContent = request.Content.Trim();
        if (trimmedTitle.Length < 5 || trimmedTitle.Length > 200)
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Tiêu đề bài viết phải từ 5 đến 200 ký tự." });
        }
        if (trimmedContent.Length < 10 || trimmedContent.Length > 5000)
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Nội dung bài viết phải từ 10 đến 5000 ký tự." });
        }

        var category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        if (!AllowedCategories.Contains(category))
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Phân loại bài viết không hợp lệ." });
        }

        // BR-57: Validate homeroom authority (chỉ GVCN của lớp mới được đăng bài)
        var homeroomClass = await _context.Classes.FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);
        if (homeroomClass == null)
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG43);
        }

        var staff = await _context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken);
        if (staff == null || homeroomClass.HomeroomTeacherId != staff.Id)
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG20);
        }

        var post = new ClassPost
        {
            ClassId = request.ClassId,
            AuthorUserId = teacherUserId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Category = category,
            IsPinned = request.IsPinned,
            AllowComments = request.AllowComments,
            CreatedAt = DateTime.UtcNow
        };

        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx", ".xls", ".xlsx" };

        var totalAttachmentCount = (request.Attachments?.Count ?? 0)
            + (request.AttachmentUrls?.Count(u => !string.IsNullOrWhiteSpace(u)) ?? 0);
        if (totalAttachmentCount > MaxAttachmentsPerPost)
        {
            return ApiResponse<ClassPostDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Mỗi bài viết chỉ được đính kèm tối đa {MaxAttachmentsPerPost} tệp hoặc liên kết." });
        }

        if (request.Attachments != null && request.Attachments.Count > 0)
        {
            foreach (var att in request.Attachments)
            {
                var trimmedUrl = att.FileUrl?.Trim() ?? string.Empty;
                if (trimmedUrl.Length > UploadValidationHelper.MaxAttachmentUrlLength || !IsSafeInternalAttachmentUrl(trimmedUrl))
                {
                    return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Đường dẫn tệp đính kèm không hợp lệ." });
                }

                var internalPath = TryExtractInternalContactBookPath(trimmedUrl);
                if (internalPath != null && _fileStorageService != null && !_fileStorageService.FileExists(internalPath))
                {
                    return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Tệp đính kèm không tồn tại trên hệ thống. Vui lòng tải tệp lên trước khi đăng bài." });
                }

                if (!UploadValidationHelper.IsSafeFileNameLength(att.FileName))
                {
                    return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { $"Tên tệp đính kèm không hợp lệ hoặc vượt quá {UploadValidationHelper.MaxFileNameLength} ký tự." });
                }

                if (att.FileSizeBytes <= 0 || att.FileSizeBytes > 5 * 1024 * 1024)
                {
                    return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG41, new List<string> { "Kích thước tệp đính kèm phải lớn hơn 0 và không vượt quá 5MB." });
                }

                var cleanFileName = UploadValidationHelper.SanitizeFileName(att.FileName);
                var ext = System.IO.Path.GetExtension(cleanFileName).ToLowerInvariant();
                if (!allowedExts.Contains(ext))
                {
                    return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG41);
                }

                post.Attachments.Add(new PostAttachment
                {
                    FileUrl = trimmedUrl,
                    FileName = cleanFileName,
                    FileType = att.FileType ?? (ext is ".jpg" or ".jpeg" or ".png" ? "Image" : "Document"),
                    FileSizeBytes = att.FileSizeBytes,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (request.AttachmentUrls != null && request.AttachmentUrls.Count > 0)
        {
            foreach (var rawUrl in request.AttachmentUrls)
            {
                if (!string.IsNullOrWhiteSpace(rawUrl))
                {
                    var url = rawUrl.Trim();
                    if (url.Length > UploadValidationHelper.MaxAttachmentUrlLength || !IsSafeExternalOrInternalUrl(url))
                    {
                        return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Liên kết đính kèm không hợp lệ hoặc không an toàn." });
                    }

                    var internalPath = TryExtractInternalContactBookPath(url);
                    if (internalPath != null && _fileStorageService != null && !_fileStorageService.FileExists(internalPath))
                    {
                        return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Tệp đính kèm không tồn tại trên hệ thống. Vui lòng tải tệp lên trước khi đăng bài." });
                    }

                    var isYouTube = url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                                    url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);

                    string fileType;
                    string fileName;

                    if (isYouTube)
                    {
                        fileType = "Video";
                        fileName = "Video YouTube";
                    }
                    else
                    {
                        var ext = System.IO.Path.GetExtension(internalPath ?? url).ToLowerInvariant();
                        if (!string.IsNullOrEmpty(ext) && ext.Length <= 5 && !allowedExts.Contains(ext))
                        {
                            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG41);
                        }

                        var isImg = ext is ".jpg" or ".jpeg" or ".png" or ".webp";
                        fileType = isImg ? "Image" : "Document";

                        try
                        {
                            var uri = new Uri(url);
                            var lastSeg = uri.Segments.LastOrDefault()?.Trim('/');
                            fileName = !string.IsNullOrWhiteSpace(lastSeg) ? Uri.UnescapeDataString(lastSeg) : "Liên kết đính kèm";
                        }
                        catch
                        {
                            var fn = System.IO.Path.GetFileName(internalPath ?? url);
                            fileName = !string.IsNullOrWhiteSpace(fn) ? fn : "Liên kết đính kèm";
                        }

                        if (fileName.Length > UploadValidationHelper.MaxFileNameLength)
                        {
                            fileName = fileName[..UploadValidationHelper.MaxFileNameLength];
                        }
                    }

                    post.Attachments.Add(new PostAttachment
                    {
                        FileUrl = url,
                        FileName = fileName,
                        FileType = fileType,
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
            .Where(sg => sg.Guardian.UserId != null) // guardian records without an account get no notification
            .Select(sg => sg.Guardian.UserId!.Value)
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
                LinkUrl = "/parent/contact-book",
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
            CreatedAt = UtcDateTimeHelper.Normalize(post.CreatedAt),
            ReactionCount = 0,
            UserHasReacted = false,
            Attachments = post.Attachments.Select(a => new PostAttachmentDto
            {
                Id = a.Id,
                FileUrl = a.FileUrl,
                FileName = a.FileName,
                FileType = a.FileType,
                FileSizeBytes = a.FileSizeBytes
            }).ToList()
        };

        return ApiResponse<ClassPostDto>.Ok(dto, SystemMessages.MSG51);
    }

    public async Task<ApiResponse<ClassPostDto>> UpdateClassPostAsync(int teacherUserId, int postId, UpdateClassPostDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Tiêu đề và nội dung bài viết không được để trống." });
        }

        var trimmedTitle = request.Title.Trim();
        var trimmedContent = request.Content.Trim();
        if (trimmedTitle.Length < 5 || trimmedTitle.Length > 200)
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Tiêu đề bài viết phải từ 5 đến 200 ký tự." });
        }
        if (trimmedContent.Length < 10 || trimmedContent.Length > 5000)
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Nội dung bài viết phải từ 10 đến 5000 ký tự." });
        }

        var category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        if (!AllowedCategories.Contains(category))
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG01, new List<string> { "Phân loại bài viết không hợp lệ." });
        }

        var post = await _context.ClassPosts
            .Include(p => p.Class)
            .Include(p => p.Attachments)
            .Include(p => p.Reactions)
            .FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post == null)
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG43);
        }

        var staff = await _context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken);
        if (staff == null || post.Class.HomeroomTeacherId != staff.Id)
        {
            return ApiResponse<ClassPostDto>.Fail(SystemMessages.MSG20);
        }

        post.Title = trimmedTitle;
        post.Content = trimmedContent;
        post.Category = category;
        post.IsPinned = request.IsPinned;
        post.AllowComments = request.AllowComments;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var authorUser = await _context.Users.FindAsync(new object[] { post.AuthorUserId }, cancellationToken);

        return ApiResponse<ClassPostDto>.Ok(new ClassPostDto
        {
            Id = post.Id,
            ClassId = post.ClassId,
            ClassName = post.Class.ClassName,
            AuthorUserId = post.AuthorUserId,
            AuthorName = authorUser?.FullName ?? "Giáo viên",
            Title = post.Title,
            Content = post.Content,
            Category = post.Category,
            IsPinned = post.IsPinned,
            AllowComments = post.AllowComments,
            CreatedAt = UtcDateTimeHelper.Normalize(post.CreatedAt),
            UpdatedAt = UtcDateTimeHelper.Normalize(post.UpdatedAt),
            ReactionCount = post.Reactions.Count,
            Attachments = post.Attachments.Select(a => new PostAttachmentDto
            {
                Id = a.Id,
                FileUrl = a.FileUrl,
                FileName = a.FileName,
                FileType = a.FileType,
                FileSizeBytes = a.FileSizeBytes
            }).ToList()
        }, SystemMessages.MSG08);
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

    public async Task<ApiResponse<bool>> ToggleCommentsLockAsync(int teacherUserId, int postId, CancellationToken cancellationToken = default)
    {
        var post = await _context.ClassPosts.Include(p => p.Class).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post == null) return ApiResponse<bool>.Fail(SystemMessages.MSG43);

        var staff = await _context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken);
        if (staff == null || post.Class.HomeroomTeacherId != staff.Id)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG20);
        }

        post.AllowComments = !post.AllowComments;
        post.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(post.AllowComments, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> DeleteClassPostAsync(int teacherUserId, int postId, CancellationToken cancellationToken = default)
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

        var trimmedContent = request.Content.Trim();
        if (trimmedContent.Length > 1000)
        {
            return ApiResponse<PostCommentDto>.Fail(SystemMessages.MSG01, new List<string> { "Bình luận không được vượt quá 1000 ký tự." });
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

        // Chỉ GVCN của lớp hoặc phụ huynh có con đang học lớp đó mới được bình luận
        var staff = await _context.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == userId, cancellationToken);
        bool isHomeroom = staff != null && post.Class.HomeroomTeacherId == staff.Id;
        string roleLabel;

        if (isHomeroom)
        {
            roleLabel = "Giáo viên chủ nhiệm";
        }
        else
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

        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);

        var comment = new PostComment
        {
            PostId = postId,
            UserId = userId,
            Content = trimmedContent,
            CreatedAt = DateTime.UtcNow
        };

        await _context.PostComments.AddAsync(comment, cancellationToken);

        // Bắn thông báo cho GVCN tác giả bài đăng nếu người bình luận không phải chính tác giả (cắt ngắn preview tránh lỗi độ dài DB)
        if (post.AuthorUserId != userId)
        {
            var titlePreview = Truncate(post.Title, 80);
            var commentPreview = Truncate(comment.Content, 150);
            _context.Notifications.Add(new Notification
            {
                UserId = post.AuthorUserId,
                Title = Truncate($"Bình luận mới từ {roleLabel}", 150),
                Content = Truncate($"{user?.FullName ?? "Phụ huynh"} vừa bình luận bài \"{titlePreview}\": \"{commentPreview}\"", 450),
                Type = "PostComment",
                ReferenceKey = $"POST:{post.Id}:CMT:{Guid.NewGuid():N}",
                LinkUrl = "/teacher/contact-book",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new PostCommentDto
        {
            Id = comment.Id,
            PostId = comment.PostId,
            UserId = comment.UserId,
            AuthorName = user?.FullName ?? "Người dùng",
            AuthorRoleLabel = roleLabel,
            Content = comment.Content,
            CreatedAt = UtcDateTimeHelper.Normalize(comment.CreatedAt),
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

        var commentContent = comment.Content;
        _context.PostComments.Remove(comment);
        await _context.SaveChangesAsync(cancellationToken);

        // BR-58: Ghi nhật ký hệ thống khi xóa bình luận
        await _auditService.LogActionAsync(
            currentUserId,
            AuditActions.PostCommentDelete,
            nameof(PostComment),
            commentId.ToString(),
            new { comment.PostId, comment.UserId, Content = commentContent },
            null,
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> ToggleReactionAsync(int userId, int postId, string reactionType, CancellationToken cancellationToken = default)
    {
        var normalizedReaction = string.IsNullOrWhiteSpace(reactionType) ? "Heart" : reactionType.Trim();
        if (!AllowedReactions.Contains(normalizedReaction))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Loại cảm xúc không hợp lệ." });
        }

        var post = await _context.ClassPosts.Include(p => p.Class).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG43);
        }

        var staff = await _context.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == userId, cancellationToken);
        bool isHomeroom = staff != null && post.Class.HomeroomTeacherId == staff.Id;
        if (!isHomeroom)
        {
            bool isLinkedParent = await _context.StudentGuardians.AnyAsync(sg =>
                sg.Guardian.UserId == userId &&
                _context.ClassAssignments.Any(ca => ca.StudentId == sg.StudentId && ca.ClassId == post.ClassId && ca.IsActive),
                cancellationToken);
            if (!isLinkedParent)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG10);
            }
        }

        var existing = await _context.PostReactions.FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, cancellationToken);
        if (existing != null)
        {
            if (string.Equals(existing.ReactionType, normalizedReaction, StringComparison.OrdinalIgnoreCase))
            {
                _context.PostReactions.Remove(existing);
            }
            else
            {
                existing.ReactionType = normalizedReaction;
            }
        }
        else
        {
            _context.PostReactions.Add(new PostReaction
            {
                PostId = postId,
                UserId = userId,
                ReactionType = normalizedReaction,
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

        var roleNames = await _context.UserRoles
            .Where(ur => ur.UserId == teacherUserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken);

        bool isPrivileged = roleNames.Contains("OfficeStaff") || roleNames.Contains("SchoolBoard");
        if (!isPrivileged)
        {
            var staff = await _context.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken);
            if (staff == null || targetClass.HomeroomTeacherId != staff.Id)
            {
                return ApiResponse<List<StudentEvaluationRosterItemDto>>.Fail(SystemMessages.MSG10);
            }
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

        var roleNames = await _context.UserRoles
            .Where(ur => ur.UserId == currentUserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken);

        bool isPrivileged = roleNames.Contains("OfficeStaff") || roleNames.Contains("SchoolBoard");

        if (!isPrivileged)
        {
            var staff = await _context.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == currentUserId, cancellationToken);
            bool isHomeroom = staff != null && await _context.ClassAssignments.AnyAsync(
                ca => ca.StudentId == studentId && ca.IsActive && ca.Class.HomeroomTeacherId == staff.Id,
                cancellationToken);

            if (!isHomeroom)
            {
                bool isParent = await _context.StudentGuardians.AnyAsync(sg => sg.Guardian.UserId == currentUserId && sg.StudentId == studentId, cancellationToken);
                if (!isParent)
                {
                    return ApiResponse<List<StudentEvaluationDto>>.Fail(SystemMessages.MSG10);
                }
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
        // TC_CTB_16 & TC_CTB_25: Bắt buộc phải có cả xếp loại nề nếp và lời nhận xét của giáo viên
        if (string.IsNullOrWhiteSpace(request.Rating) || string.IsNullOrWhiteSpace(request.TeacherNote))
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG01);
        }

        var rating = request.Rating.Trim();
        if (!AllowedRatings.Contains(rating))
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG01, new List<string> { "Xếp loại nề nếp không hợp lệ (chỉ chấp nhận Excellent, Good, NeedsAttention)." });
        }

        if (request.WeekNumber < 1 || request.WeekNumber > 52)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG01, new List<string> { "Tuần học phải nằm trong khoảng 1 đến 52." });
        }

        var trimmedNote = request.TeacherNote.Trim();
        if (trimmedNote.Length < 10 || trimmedNote.Length > 1500)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG01, new List<string> { "Lời nhận xét của giáo viên phải từ 10 đến 1500 ký tự." });
        }

        if ((request.PrivateRemind?.Trim().Length ?? 0) > 500)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG01, new List<string> { "Dặn dò riêng tối đa 500 ký tự." });
        }

        var targetClass = await _context.Classes.FindAsync(new object[] { request.ClassId }, cancellationToken);
        var student = await _context.Students.FindAsync(new object[] { request.StudentId }, cancellationToken);
        if (targetClass == null || student == null)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG11);
        }

        var schoolYear = targetClass.SchoolYearId > 0
            ? await _context.SchoolYears.FindAsync(new object[] { targetClass.SchoolYearId }, cancellationToken)
            : null;
        if (schoolYear != null)
        {
            var start = schoolYear.StartDate.Date;
            int diff = ((int)start.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var week1Monday = start.AddDays(-diff);
            var totalWeeks = Math.Clamp((int)Math.Ceiling((schoolYear.EndDate.Date - week1Monday).TotalDays / 7.0), 1, 52);
            if (request.WeekNumber > totalWeeks)
            {
                return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG01, new List<string> { $"Tuần học phải nằm trong phạm vi năm học (từ tuần 1 đến tuần {totalWeeks})." });
            }

            var vnToday = DateTime.UtcNow.AddHours(7).Date;
            if (vnToday >= week1Monday)
            {
                var currentWeek = Math.Max(1, (int)((vnToday - week1Monday).TotalDays / 7) + 1);
                if (request.WeekNumber > currentWeek)
                {
                    return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG01, new List<string> { $"Không thể nhận xét cho tuần tương lai (hiện tại là tuần {currentWeek})." });
                }
            }
        }

        bool hasAnyActiveAssignment = await _context.ClassAssignments.AnyAsync(
            ca => ca.StudentId == request.StudentId && ca.IsActive,
            cancellationToken);
        if (hasAnyActiveAssignment)
        {
            bool studentInClass = await _context.ClassAssignments.AnyAsync(
                ca => ca.StudentId == request.StudentId && ca.ClassId == request.ClassId && ca.IsActive,
                cancellationToken);
            if (!studentInClass)
            {
                return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG01, new List<string> { "Học sinh không thuộc lớp học đã chọn." });
            }
        }

        // BR-57: Check homeroom teacher (chỉ GVCN của lớp mới được tạo/sửa phiếu nhận xét)
        var staff = await _context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == teacherUserId, cancellationToken);
        if (staff == null || targetClass.HomeroomTeacherId != staff.Id)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG20);
        }

        // BR-61 (BUG-CB-04): Check existing evaluation for this week scoped by SchoolYearId
        var existing = await _context.StudentEvaluations
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == request.StudentId
                && e.WeekNumber == request.WeekNumber
                && ((e.SchoolYearId ?? e.Class.SchoolYearId) == targetClass.SchoolYearId),
                cancellationToken);

        if (existing != null)
        {
            // Nếu không truyền ID sửa hoặc ID không khớp, từ chối tạo trùng phiếu (BR-61)
            if (!request.Id.HasValue || request.Id.Value == 0 || request.Id.Value != existing.Id)
            {
                return ApiResponse<StudentEvaluationDto>.Fail("Mỗi học sinh chỉ có tối đa 01 phiếu nhận xét cho mỗi tuần học (BR-61). Bản ghi hiện tại không thay đổi.");
            }

            // BR-59: Lock if already acknowledged
            if (existing.IsAcknowledged)
            {
                return ApiResponse<StudentEvaluationDto>.Fail(SystemMessages.MSG09);
            }

            existing.SchoolYearId ??= targetClass.SchoolYearId;
            existing.Rating = rating;
            existing.BadgesJson = JsonSerializer.Serialize(request.Badges ?? new List<string>());
            existing.TeacherNote = request.TeacherNote.Trim();
            existing.PrivateRemind = request.PrivateRemind?.Trim();
            existing.EvaluationDate = request.EvaluationDate;
            existing.UpdatedAt = DateTime.UtcNow;

            var updateGuardianUserIds = await _context.StudentGuardians
                .Where(sg => sg.StudentId == request.StudentId && sg.Guardian.UserId != null)
                .Select(sg => sg.Guardian.UserId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            var evalRefKey = $"EVAL:{existing.Id}";
            var existingNotifs = await _context.Notifications
                .Where(n => updateGuardianUserIds.Contains(n.UserId) && n.ReferenceKey == evalRefKey)
                .ToListAsync(cancellationToken);
            var existingNotifMap = existingNotifs.ToDictionary(n => n.UserId);

            foreach (var gUid in updateGuardianUserIds)
            {
                var title = $"Cập nhật phiếu nhận xét tuần {request.WeekNumber} của em {student.FullName}";
                var content = $"GVCN đã cập nhật nhận xét nề nếp tuần {request.WeekNumber}. Vui lòng xem lại và ký xác nhận.";
                if (existingNotifMap.TryGetValue(gUid, out var existingNotif))
                {
                    existingNotif.Title = title;
                    existingNotif.Content = content;
                    existingNotif.IsRead = false;
                    existingNotif.CreatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = gUid,
                        Title = title,
                        Content = content,
                        Type = "StudentEvaluation",
                        ReferenceKey = evalRefKey,
                        LinkUrl = "/parent/contact-book",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return ApiResponse<StudentEvaluationDto>.Ok(MapToDto(existing, student.FullName, student.StudentCode, targetClass.ClassName), SystemMessages.MSG52);
        }

        var newEval = new StudentEvaluation
        {
            StudentId = request.StudentId,
            ClassId = request.ClassId,
            SchoolYearId = targetClass.SchoolYearId,
            TeacherUserId = teacherUserId,
            EvaluationDate = request.EvaluationDate,
            WeekNumber = request.WeekNumber,
            Rating = rating,
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
            .Where(sg => sg.Guardian.UserId != null) // guardian records without an account get no notification
            .Select(sg => sg.Guardian.UserId!.Value)
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
                LinkUrl = "/parent/contact-book",
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

        var cleanFeedback = request.ParentFeedback?.Trim();
        if (!string.IsNullOrEmpty(cleanFeedback) && cleanFeedback.Length > 500)
        {
            return ApiResponse<StudentEvaluationDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Phản hồi của phụ huynh không được vượt quá 500 ký tự." });
        }

        eval.IsAcknowledged = true;
        eval.AcknowledgedAt = DateTime.UtcNow;
        eval.AcknowledgedByGuardianId = guardian.Id;
        eval.ParentFeedback = string.IsNullOrEmpty(cleanFeedback) ? null : cleanFeedback;
        eval.UpdatedAt = DateTime.UtcNow;

        // Thông báo lại cho GVCN
        _context.Notifications.Add(new Notification
        {
            UserId = eval.TeacherUserId,
            Title = $"Phụ huynh đã ký nhận phiếu nhận xét",
            Content = $"Phụ huynh em {eval.Student.FullName} đã xác nhận phiếu tuần {eval.WeekNumber}." +
                      (!string.IsNullOrWhiteSpace(eval.ParentFeedback) ? $" Phản hồi: \"{eval.ParentFeedback}\"" : ""),
            Type = "EvaluationAcknowledged",
            ReferenceKey = $"EVAL:{eval.Id}:ACK:{Guid.NewGuid():N}",
            LinkUrl = "/teacher/contact-book",
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
            EvaluationDate = UtcDateTimeHelper.Normalize(e.EvaluationDate),
            WeekNumber = e.WeekNumber,
            Rating = e.Rating,
            Badges = badges,
            TeacherNote = e.TeacherNote,
            PrivateRemind = e.PrivateRemind,
            IsAcknowledged = e.IsAcknowledged,
            AcknowledgedAt = UtcDateTimeHelper.Normalize(e.AcknowledgedAt),
            AcknowledgedByGuardianName = e.AcknowledgedByGuardian?.FullName,
            ParentFeedback = e.ParentFeedback,
            CreatedAt = UtcDateTimeHelper.Normalize(e.CreatedAt)
        };
    }
}
