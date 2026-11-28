using Core.Common;
using Core.Configuration;
using Core.Enums;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations;

public class AdminService : IAdminService
{
    private static readonly string[] StaffRoleNames = { "Teacher", "OfficeStaff", "ServiceStaff", "SchoolBoard" };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IAccountLinkService _accountLinks;
    private readonly SchoolOptions _options;

    public AdminService(IUnitOfWork unitOfWork, IAuditService auditService, IAccountLinkService accountLinks, IOptions<SchoolOptions>? options = null)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _accountLinks = accountLinks;
        _options = options?.Value ?? new SchoolOptions();
    }

    public async Task<ApiResponse<PaginatedResult<AdminUserDto>>> GetUsersPagedAsync(
        int pageIndex, int pageSize, string? role, bool? isActive, string? search, CancellationToken ct = default)
    {
        var page = Math.Max(1, pageIndex);
        var size = Math.Clamp(pageSize, 1, 100);

        var query = _unitOfWork.Repository<User>()
            .Query()
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim().ToLower();
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name.ToLower() == r));
        }

        if (isActive.HasValue)
        {
            query = isActive.Value
                ? query.Where(u => u.Status == AccountStatus.Active)
                : query.Where(u => u.Status != AccountStatus.Active);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(q) ||
                u.FullName.ToLower().Contains(q) ||
                u.Email.ToLower().Contains(q) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(q)));
        }

        var total = await query.CountAsync(ct);
        if (total == 0 || (page - 1) * size >= total)
        {
            return ApiResponse<PaginatedResult<AdminUserDto>>.Ok(
                new PaginatedResult<AdminUserDto>(new List<AdminUserDto>(), total, page, size));
        }

        // Two-step projection avoids sorting NVARCHAR(MAX) PasswordHash (eliminates 52MB memory grants)
        var pageIds = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var rows = await _unitOfWork.Repository<User>()
            .Query()
            .AsNoTracking()
            .Where(u => pageIds.Contains(u.Id))
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                FullName = u.FullName,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.Status == AccountStatus.Active,
                Status = u.Status.ToString(),
                CanDelete = u.Status == AccountStatus.PendingActivation || (u.Status == AccountStatus.Deactivated && string.IsNullOrEmpty(u.PasswordHash)),
                CreatedAt = u.CreatedAt,
                Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                StaffCode = u.StaffProfile != null ? u.StaffProfile.StaffCode : null,
                Position = u.StaffProfile != null ? u.StaffProfile.Position : null,
                Department = u.StaffProfile != null ? u.StaffProfile.Department : null
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            row.CreatedAt = UtcDateTimeHelper.Normalize(row.CreatedAt);
        }

        var orderMap = pageIds
            .Select((id, idx) => new { id, idx })
            .ToDictionary(x => x.id, x => x.idx);

        var items = rows.OrderBy(r => orderMap[r.Id]).ToList();
        return ApiResponse<PaginatedResult<AdminUserDto>>.Ok(new PaginatedResult<AdminUserDto>(items, total, page, size));
    }

    public async Task<ApiResponse<PaginatedResult<AdminUserDto>>> GetStaffPagedAsync(
        int pageIndex, int pageSize, string? role, bool? isActive, string? search, CancellationToken ct = default)
    {
        var page = Math.Max(1, pageIndex);
        var size = Math.Clamp(pageSize, 1, 100);

        var query = _unitOfWork.Repository<User>()
            .Query()
            .AsNoTracking()
            .Where(u =>
                u.StaffProfile != null ||
                u.UserRoles.Any(ur => StaffRoleNames.Contains(ur.Role.Name) || ur.Role.Name == "Admin"));

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim().ToLower();
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name.ToLower() == r));
        }

        if (isActive.HasValue)
        {
            query = isActive.Value
                ? query.Where(u => u.Status == AccountStatus.Active)
                : query.Where(u => u.Status != AccountStatus.Active);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(q) ||
                u.FullName.ToLower().Contains(q) ||
                u.Email.ToLower().Contains(q) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(q)) ||
                (u.StaffProfile != null && (
                    u.StaffProfile.StaffCode.ToLower().Contains(q) ||
                    u.StaffProfile.Department.ToLower().Contains(q) ||
                    u.StaffProfile.Position.ToLower().Contains(q))));
        }

        var total = await query.CountAsync(ct);
        if (total == 0 || (page - 1) * size >= total)
        {
            return ApiResponse<PaginatedResult<AdminUserDto>>.Ok(
                new PaginatedResult<AdminUserDto>(new List<AdminUserDto>(), total, page, size));
        }

        var pageIds = await query
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var rows = await _unitOfWork.Repository<User>()
            .Query()
            .AsNoTracking()
            .Where(u => pageIds.Contains(u.Id))
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                FullName = u.FullName,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.Status == AccountStatus.Active,
                Status = u.Status.ToString(),
                CanDelete = u.Status == AccountStatus.PendingActivation || (u.Status == AccountStatus.Deactivated && string.IsNullOrEmpty(u.PasswordHash)),
                CreatedAt = u.CreatedAt,
                Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                StaffCode = u.StaffProfile != null ? u.StaffProfile.StaffCode : null,
                Position = u.StaffProfile != null ? u.StaffProfile.Position : null,
                Department = u.StaffProfile != null ? u.StaffProfile.Department : null,
                HomeroomClassName = u.StaffProfile != null
                    ? u.StaffProfile.HomeroomClasses
                        .OrderByDescending(c => c.Id)
                        .Select(c => c.ClassName)
                        .FirstOrDefault()
                    : null
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            row.CreatedAt = UtcDateTimeHelper.Normalize(row.CreatedAt);
        }

        var orderMap = pageIds
            .Select((id, idx) => new { id, idx })
            .ToDictionary(x => x.id, x => x.idx);

        var items = rows.OrderBy(r => orderMap[r.Id]).ToList();
        return ApiResponse<PaginatedResult<AdminUserDto>>.Ok(new PaginatedResult<AdminUserDto>(items, total, page, size));
    }

    public async Task<ApiResponse<List<RoleOptionDto>>> GetRolesAsync(CancellationToken ct = default)
    {
        var roles = await _unitOfWork.Repository<Role>()
            .Query()
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new RoleOptionDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description
            })
            .ToListAsync(ct);

        return ApiResponse<List<RoleOptionDto>>.Ok(roles);
    }

    public async Task<ApiResponse<AdminUserDto>> CreateUserAsync(int adminUserId, CreateUserRequestDto request, CancellationToken ct = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;
        var rawFullName = request.FullName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(rawFullName))
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập đầy đủ Tên đăng nhập, Email và Họ tên." });
        }

        var usernameErr = StudentValidationHelper.ValidateUsername(username);
        if (usernameErr != null)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { usernameErr });
        }

        var fullNameErr = StudentValidationHelper.ValidateFullName(rawFullName, "họ và tên");
        if (fullNameErr != null)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { fullNameErr });
        }
        var fullName = StudentValidationHelper.NormalizeWhitespace(rawFullName);

        if (!StudentValidationHelper.IsValidEmail(email))
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Email không hợp lệ." });
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !StudentValidationHelper.IsValidPhone(request.PhoneNumber))
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Số điện thoại không hợp lệ (cần gồm 10 chữ số và bắt đầu bằng số 0)." });
        }

        if (StudentValidationHelper.ContainsHtmlTags(request.StaffCode)
            || StudentValidationHelper.ContainsHtmlTags(request.Position)
            || StudentValidationHelper.ContainsHtmlTags(request.Department))
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Thông tin nhân sự không được chứa thẻ HTML." });
        }

        // pub:UC-53 step 3: the Administrator selects one initial role
        var requestedRoleNames = (request.Roles ?? new List<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToLower()).Distinct().ToList();
        if (requestedRoleNames.Count != 1)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn đúng một vai trò." });
        }

        var exists = await _unitOfWork.Repository<User>()
            .Query()
            .AnyAsync(u => u.Status != AccountStatus.Deactivated
                           && (u.Username.ToLower() == username.ToLower() || u.Email.ToLower() == email.ToLower()), ct);

        if (exists)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Tên đăng nhập hoặc Email đã tồn tại trong hệ thống." });
        }

        var dbRoles = await _unitOfWork.Repository<Role>()
            .Query()
            .Where(r => requestedRoleNames.Contains(r.Name.ToLower()))
            .ToListAsync(ct);

        if (dbRoles.Count == 0)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Vai trò được chọn không hợp lệ." });
        }

        // UC-53 A3, BR-66: Parent accounts come from guardian records through account requests, not from here
        if (dbRoles.Any(r => r.Name.Equals(UserRoles.Parent.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Tài khoản phụ huynh được tạo từ hồ sơ người giám hộ qua yêu cầu cấp tài khoản." });
        }

        var user = new User
        {
            Username = username,
            Email = email,
            FullName = fullName,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            // BR-63, UC-53 step 5: no password; starts Disabled (Deactivated) unless EnableNow is checked
            PasswordHash = string.Empty,
            Status = request.EnableNow ? AccountStatus.PendingActivation : AccountStatus.Deactivated,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var role in dbRoles)
        {
            user.UserRoles.Add(new UserRole { RoleId = role.Id, Role = role });
        }

        if (dbRoles.Any(r => StaffRoleNames.Contains(r.Name, StringComparer.OrdinalIgnoreCase)))
        {
            var code = string.IsNullOrWhiteSpace(request.StaffCode)
                ? $"STF{DateTime.UtcNow:yyMMddHHmmss}"
                : request.StaffCode.Trim();
            user.StaffProfile = new StaffProfile
            {
                StaffCode = code,
                Position = string.IsNullOrWhiteSpace(request.Position) ? dbRoles.First().Name : request.Position.Trim(),
                Department = string.IsNullOrWhiteSpace(request.Department) ? "Nhà trường" : request.Department.Trim(),
                IsHomeroomTeacher = dbRoles.Any(r => r.Name.Equals("Teacher", StringComparison.OrdinalIgnoreCase)),
                CreatedAt = DateTime.UtcNow
            };
        }

        await _unitOfWork.Repository<User>().AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.UserCreate,
            nameof(User),
            user.Id.ToString(),
            null,
            new { user.Username, user.FullName, user.Email, Roles = dbRoles.Select(r => r.Name), request.EnableNow },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var result = ApiResponse<AdminUserDto>.Ok(MapToUserDto(user), SystemMessages.MSG38);
        if (request.EnableNow)
        {
            if (await _accountLinks.SendActivationLinkAsync(user, ct))
            {
                await _auditService.LogActionAsync(adminUserId, AuditActions.UserActivationLinkSent, nameof(User), user.Id.ToString(),
                    null, new { user.Email }, null, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            else
            {
                result.Errors = new List<string> { SystemMessages.MSG59.Content };
            }
        }
        return result;
    }

    public async Task<ApiResponse<AdminUserDto>> UpdateUserRolesAsync(int adminUserId, int targetUserId, UpdateUserRolesRequestDto request, CancellationToken ct = default)
    {
        if (request.Roles == null || request.Roles.Count == 0)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Người dùng phải có ít nhất một vai trò." });
        }

        var user = await _unitOfWork.Repository<User>()
            .Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.StaffProfile)
            .Include(u => u.Guardian)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        if (user == null)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy tài khoản người dùng." });
        }

        if (user.Id == adminUserId)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG10, new List<string> { "Không thể tự thay đổi vai trò của tài khoản đang đăng nhập." });
        }

        if (await HasAssociatedOperationalDataAsync(user, ct))
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG64);
        }

        var requestedNames = request.Roles.Select(r => r.Trim().ToLower()).Distinct().ToList();
        var dbRoles = await _unitOfWork.Repository<Role>()
            .Query()
            .Where(r => requestedNames.Contains(r.Name.ToLower()))
            .ToListAsync(ct);

        if (dbRoles.Count == 0)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Vai trò không hợp lệ." });
        }

        // OVR-04 / BR-68: a staff account is never a Parent account, so Parent is never combined with another role
        if (dbRoles.Count > 1 && dbRoles.Any(r => r.Name.Equals(UserRoles.Parent.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Không thể kết hợp vai trò Phụ huynh với vai trò khác." });
        }

        if (user.Id == adminUserId && !dbRoles.Any(r => r.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG09, new List<string> { "Không thể tự gỡ vai trò Admin của tài khoản đang đăng nhập." });
        }

        var oldRoles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        var existingUserRoles = user.UserRoles.ToList();
        foreach (var ur in existingUserRoles)
        {
            await _unitOfWork.Repository<UserRole>().DeleteAsync(ur, ct);
        }
        user.UserRoles.Clear();

        foreach (var role in dbRoles)
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        }

        if (user.StaffProfile == null && dbRoles.Any(r => StaffRoleNames.Contains(r.Name, StringComparer.OrdinalIgnoreCase)))
        {
            user.StaffProfile = new StaffProfile
            {
                UserId = user.Id,
                StaffCode = $"STF{user.Id:D4}",
                Position = dbRoles.First().Name,
                Department = "Nhà trường",
                IsHomeroomTeacher = dbRoles.Any(r => r.Name.Equals("Teacher", StringComparison.OrdinalIgnoreCase)),
                CreatedAt = DateTime.UtcNow
            };
        }

        user.UpdatedAt = DateTime.UtcNow;
        if (user.Id != adminUserId)
        {
            user.SessionVersion++; // roles live in the login token: the user signs in again to get the new ones
        }

        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.UserUpdateRoles,
            nameof(User),
            user.Id.ToString(),
            new { Roles = oldRoles },
            new { Roles = dbRoles.Select(r => r.Name) },
            null, // TODO: pass the client IP address; it is not recorded yet.
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<AdminUserDto>.Ok(MapToUserDto(user), SystemMessages.MSG40);
    }

    public async Task<ApiResponse<bool>> UpdateUserStatusAsync(int adminUserId, int targetUserId, bool isActive, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(targetUserId, ct);
        if (user == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy tài khoản người dùng." });
        }

        if (user.Id == adminUserId && !isActive)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Không thể tự vô hiệu hóa tài khoản đang đăng nhập." });
        }

        if (isActive && user.Status == AccountStatus.PendingActivation)
        {
            // Only the holder activates the account, by setting a password through the link (BR-63)
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Tài khoản đang chờ kích hoạt qua email; người dùng có thể nhận liên kết mới bằng \"Quên mật khẩu\"." });
        }

        var now = DateTime.UtcNow;
        var oldStatus = user.Status;
        var sendActivation = false;

        if (isActive)
        {
            // BR-64 (UC-53 A4 E2): ensure no other non-deactivated account uses this email
            var emailTaken = await _unitOfWork.Repository<User>().Query()
                .AnyAsync(u => u.Id != user.Id && u.Status != AccountStatus.Deactivated && u.Email.ToLower() == user.Email.ToLower(), ct);
            if (emailTaken)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG66);
            }

            // UC-53 A1 / A4: an account that never set a password becomes PendingActivation and receives an activation link
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                user.Status = AccountStatus.PendingActivation;
                sendActivation = true;
            }
            else
            {
                user.Status = AccountStatus.Active;
            }
        }
        else
        {
            user.Status = AccountStatus.Deactivated;
            user.SessionVersion++; // BR-48: existing sessions end now

            // UC-53 A4: disabling invalidates any unused links sent before
            var tokenRepo = _unitOfWork.Repository<VerificationToken>();
            if (tokenRepo != null)
            {
                var openTokens = await tokenRepo.Query()
                    .Where(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now)
                    .ToListAsync(ct);
                foreach (var t in openTokens)
                {
                    t.ExpiresAt = now;
                }
            }
        }
        user.UpdatedAt = now;

        await _auditService.LogActionAsync(
            adminUserId,
            isActive ? AuditActions.UserActivate : AuditActions.UserDeactivate,
            nameof(User),
            user.Id.ToString(),
            new { Status = oldStatus.ToString() },
            new { Status = user.Status.ToString() },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        if (sendActivation)
        {
            var sent = await _accountLinks.SendActivationLinkAsync(user, ct);
            if (!sent)
            {
                return ApiResponse<bool>.Ok(true, SystemMessages.MSG59);
            }
            await _auditService.LogActionAsync(adminUserId, AuditActions.UserActivationLinkSent, nameof(User), user.Id.ToString(),
                null, new { user.Email }, null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return ApiResponse<bool>.Ok(true, isActive ? SystemMessages.MSG08 : SystemMessages.MSG39);
    }

    public async Task<ApiResponse<bool>> ChangeUserEmailAsync(int adminUserId, int targetUserId, string newEmail, CancellationToken ct = default)
    {
        if (targetUserId == adminUserId)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG10, new List<string> { "Quản trị viên không thể tự đổi email của chính mình." });
        }

        var trimmed = newEmail?.Trim() ?? string.Empty;
        if (!StudentValidationHelper.IsValidEmail(trimmed))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Địa chỉ email mới không hợp lệ." });
        }

        var user = await _unitOfWork.Repository<User>()
            .Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        if (user == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG11);
        }

        if (user.Status == AccountStatus.Deactivated)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Không thể đổi email của tài khoản đã bị vô hiệu hóa." });
        }

        if (user.UserRoles.Any(ur => ur.Role.Name.Equals(UserRoles.Parent.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG10, new List<string> { "Đổi email tài khoản Phụ huynh phải được thực hiện qua yêu cầu của Giáo vụ tại mục Duyệt yêu cầu tài khoản." });
        }

        if (string.Equals(user.Email, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Email mới phải khác email hiện tại." });
        }

        var emailTaken = await _unitOfWork.Repository<User>().Query()
            .AnyAsync(u => u.Id != user.Id && u.Status != AccountStatus.Deactivated && u.Email.ToLower() == trimmed.ToLower(), ct);
        if (emailTaken)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Địa chỉ email này đã được sử dụng bởi tài khoản khác." });
        }

        var sent = await _accountLinks.SendEmailChangeLinkAsync(user, trimmed, ct);
        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.UserChangeEmail,
            nameof(User),
            user.Id.ToString(),
            new { Email = user.Email },
            new { PendingNewEmail = trimmed, EmailSent = sent },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return sent
            ? ApiResponse<bool>.Ok(true, $"Đã gửi liên kết xác nhận đổi email tới {trimmed}.")
            : ApiResponse<bool>.Fail(SystemMessages.MSG59);
    }

    private async Task<bool> HasAssociatedOperationalDataAsync(User user, CancellationToken ct)
    {
        if (user.EmailVerifiedAt != null || !string.IsNullOrEmpty(user.PasswordHash) || user.Status == AccountStatus.Active)
        {
            return true;
        }
        if (user.Guardian != null)
        {
            return true;
        }
        var guardianRepo = _unitOfWork.Repository<Guardian>();
        if (guardianRepo != null && await guardianRepo.Query().AnyAsync(g => g.UserId == user.Id, ct))
        {
            return true;
        }
        if (user.StaffProfile != null)
        {
            var classRepo = _unitOfWork.Repository<Class>();
            if (classRepo != null && await classRepo.Query().AnyAsync(c => c.HomeroomTeacherId == user.StaffProfile.Id, ct))
            {
                return true;
            }
        }
        var attRepo = _unitOfWork.Repository<AttendanceRecord>();
        if (attRepo != null && await attRepo.Query().AnyAsync(a => a.RecordedByUserId == user.Id || a.UpdatedByUserId == user.Id, ct))
        {
            return true;
        }
        var reqRepo = _unitOfWork.Repository<AdministrativeRequest>();
        if (reqRepo != null && await reqRepo.Query().AnyAsync(r => r.RequesterUserId == user.Id || r.ReviewerUserId == user.Id, ct))
        {
            return true;
        }
        return false;
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(int adminUserId, int targetUserId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Repository<User>()
            .Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.StaffProfile)
            .Include(u => u.VerificationTokens)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        if (user == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy tài khoản người dùng." });
        }

        if (user.Id == adminUserId)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Không thể xóa tài khoản đang đăng nhập." });
        }

        var neverActivated = user.Status == AccountStatus.PendingActivation
            || (user.Status == AccountStatus.Deactivated && string.IsNullOrEmpty(user.PasswordHash));
        if (!neverActivated)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ có thể xóa tài khoản chưa từng được kích hoạt sử dụng." });
        }

        // Unlink any AccountRequestItem created for this account (Restrict FK)
        var reqItemRepo = _unitOfWork.Repository<AccountRequestItem>();
        if (reqItemRepo != null)
        {
            var linkedItems = await reqItemRepo.Query()
                .Where(i => i.CreatedUserId == user.Id)
                .ToListAsync(ct);
            foreach (var item in linkedItems)
            {
                item.CreatedUserId = null;
            }
        }

        var guardianRepo = _unitOfWork.Repository<Guardian>();
        if (guardianRepo != null)
        {
            var linkedGuardians = await guardianRepo.Query()
                .Where(g => g.UserId == user.Id)
                .ToListAsync(ct);
            foreach (var g in linkedGuardians)
            {
                g.UserId = null;
            }
        }

        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.UserDelete,
            nameof(User),
            user.Id.ToString(),
            new { user.Username, user.Email, user.FullName, Status = user.Status.ToString() },
            null,
            null,
            ct);

        await _unitOfWork.Repository<User>().DeleteAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> ResendActivationLinkAsync(int adminUserId, int targetUserId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Repository<User>()
            .Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        if (user == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy tài khoản người dùng." });
        }

        if (user.Status != AccountStatus.PendingActivation)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ có thể gửi lại liên kết kích hoạt cho tài khoản đang ở trạng thái chờ kích hoạt." });
        }

        var sent = await _accountLinks.SendActivationLinkAsync(user, ct);
        if (!sent)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG59);
        }

        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.UserActivationLinkSent,
            nameof(User),
            user.Id.ToString(),
            null,
            new { user.Email, ResentByAdmin = true },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> ResetUserPasswordAsync(int adminUserId, int targetUserId, string newPassword, CancellationToken ct = default)
    {
        if (!PasswordPolicy.IsValid(newPassword))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG04, new List<string> { "Mật khẩu mới chưa đáp ứng yêu cầu bảo mật (tối thiểu 8 ký tự, gồm hoa, thường, số, ký tự đặc biệt)." });
        }

        var user = await _unitOfWork.Users.GetByIdWithRolesAsync(targetUserId, ct);
        if (user == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy tài khoản người dùng." });
        }

        // UC-60 E1, BR-65: not for System Administrator accounts or accounts that are not Active
        var isAdministrator = user.UserRoles.Any(ur => ur.Role != null && ur.Role.Name.Equals(UserRoles.Admin.ToString(), StringComparison.OrdinalIgnoreCase));
        if (isAdministrator || user.Status != AccountStatus.Active)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG10);
        }

        // Temporary password, communicated outside SAWP; it must be changed at the next sign-in (UC-60 A1, BR-02)
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = true;
        user.SessionVersion++; // UC-60 step 5, BR-03: active sessions end
        user.UpdatedAt = DateTime.UtcNow;

        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.UserResetPassword,
            nameof(User),
            user.Id.ToString(),
            null,
            new { user.Username, ResetAt = DateTime.UtcNow },
            null, // TODO: pass the client IP address; it is not recorded yet.
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG46);
    }

    public async Task<ApiResponse<List<SchoolCalendarDayDto>>> GetCalendarDaysAsync(int? schoolYearId, string? month, CancellationToken ct = default)
    {
        int targetYearId;
        if (schoolYearId.HasValue && schoolYearId.Value > 0)
        {
            targetYearId = schoolYearId.Value;
        }
        else
        {
            var activeYear = await _unitOfWork.Repository<SchoolYear>()
                .Query()
                .AsNoTracking()
                .OrderByDescending(y => y.IsActive)
                .ThenByDescending(y => y.StartDate)
                .FirstOrDefaultAsync(ct);
            if (activeYear == null)
            {
                return ApiResponse<List<SchoolCalendarDayDto>>.Fail(SystemMessages.MSG14);
            }
            targetYearId = activeYear.Id;
        }

        var query = _unitOfWork.Repository<SchoolCalendarDay>()
            .Query()
            .Include(d => d.SchoolYear)
            .AsNoTracking()
            .Where(d => d.SchoolYearId == targetYearId);

        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse($"{month}-01", out var mStart))
        {
            var mEnd = mStart.AddMonths(1);
            query = query.Where(d => d.Date >= mStart && d.Date < mEnd);
        }

        var items = await query
            .OrderBy(d => d.Date)
            .Select(d => new SchoolCalendarDayDto
            {
                Id = d.Id,
                SchoolYearId = d.SchoolYearId,
                SchoolYearName = d.SchoolYear.YearName,
                Date = d.Date,
                IsSchoolDay = d.IsSchoolDay,
                Note = d.Note
            })
            .ToListAsync(ct);

        return ApiResponse<List<SchoolCalendarDayDto>>.Ok(items);
    }

    public async Task<ApiResponse<SchoolCalendarDayDto>> UpsertCalendarDayAsync(int adminUserId, UpdateCalendarDayDto request, CancellationToken ct = default)
    {
        if (request.Date == default || request.Date.Year < 2000 || request.Date.Year > 2100)
        {
            return ApiResponse<SchoolCalendarDayDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Ngày cấu hình lịch học không hợp lệ." });
        }

        var trimmedNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (trimmedNote != null && trimmedNote.Length > 250)
        {
            return ApiResponse<SchoolCalendarDayDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Ghi chú không được vượt quá 250 ký tự." });
        }

        var date = request.Date.Date;
        SchoolYear? targetYear;
        if (request.SchoolYearId.HasValue && request.SchoolYearId.Value > 0)
        {
            targetYear = await _unitOfWork.Repository<SchoolYear>()
                .GetByIdAsync(request.SchoolYearId.Value, ct);
            if (targetYear == null)
            {
                return ApiResponse<SchoolCalendarDayDto>.Fail(
                    SystemMessages.MSG43,
                    new List<string> { "Không tìm thấy năm học được chọn." });
            }
        }
        else
        {
            targetYear = await _unitOfWork.Repository<SchoolYear>()
                .Query()
                .OrderByDescending(y => y.IsActive)
                .ThenByDescending(y => y.StartDate)
                .FirstOrDefaultAsync(ct);
            if (targetYear == null)
            {
                return ApiResponse<SchoolCalendarDayDto>.Fail(SystemMessages.MSG14);
            }
        }

        if (date < targetYear.StartDate.Date || date > targetYear.EndDate.Date)
        {
            return ApiResponse<SchoolCalendarDayDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Ngày {date:dd-MM-yyyy} nằm ngoài phạm vi năm học {targetYear.YearName} ({targetYear.StartDate:dd-MM-yyyy} – {targetYear.EndDate:dd-MM-yyyy})." });
        }

        var yearId = targetYear.Id;

        var lunchDayRepo = _unitOfWork.Repository<LunchServiceDay>();
        LunchServiceDay? existingLunchDay = lunchDayRepo != null
            ? await lunchDayRepo.Query().FirstOrDefaultAsync(d => d.Date.Date == date, ct)
            : null;

        if (!request.IsSchoolDay && existingLunchDay != null && existingLunchDay.IsLocked)
        {
            return ApiResponse<SchoolCalendarDayDto>.Fail(
                SystemMessages.MSG27,
                new List<string> { $"Ngày {date:dd-MM-yyyy} đã khóa sổ nấu bán trú, vui lòng mở khóa sổ trước khi chuyển sang ngày nghỉ." });
        }

        var existing = await _unitOfWork.Repository<SchoolCalendarDay>()
            .Query()
            .Include(d => d.SchoolYear)
            .FirstOrDefaultAsync(d => d.SchoolYearId == yearId && d.Date.Date == date, ct);

        object? oldValues = null;
        if (existing == null)
        {
            existing = new SchoolCalendarDay
            {
                SchoolYearId = yearId,
                Date = date,
                IsSchoolDay = request.IsSchoolDay,
                Note = trimmedNote
            };
            await _unitOfWork.Repository<SchoolCalendarDay>().AddAsync(existing, ct);
        }
        else
        {
            oldValues = new { existing.IsSchoolDay, existing.Note };
            existing.IsSchoolDay = request.IsSchoolDay;
            existing.Note = trimmedNote;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Đồng bộ bán trú theo lịch trường: ngày nghỉ đóng bán trú, ngày học bù T7/CN mở bán trú
        if (lunchDayRepo != null)
        {
            var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            var lunchDay = existingLunchDay;
            if (!request.IsSchoolDay)
            {
                if (lunchDay == null)
                {
                    await lunchDayRepo.AddAsync(new LunchServiceDay
                    {
                        Date = date,
                        IsServing = false,
                        CutoffTime = _options.LunchCutoff,
                        Remarks = string.IsNullOrWhiteSpace(request.Note) ? "Đóng bán trú theo lịch nghỉ của trường" : request.Note.Trim()
                    }, ct);
                }
                else
                {
                    lunchDay.IsServing = false;
                }

                // Hủy các đăng ký bán trú đang hiệu lực vào ngày vừa đóng và thông báo Phụ huynh
                var lunchRegRepo = _unitOfWork.Repository<LunchRegistration>();
                if (lunchRegRepo != null)
                {
                    var regsOnDate = await lunchRegRepo.Query()
                        .Include(r => r.Student)
                            .ThenInclude(s => s.StudentGuardians)
                                .ThenInclude(sg => sg.Guardian)
                        .Where(r => r.Date.Date == date && r.IsActive)
                        .ToListAsync(ct);

                    var notifiedKeys = new HashSet<(int UserId, int StudentId)>();
                    var notifRepo = _unitOfWork.Repository<Notification>();

                    foreach (var reg in regsOnDate)
                    {
                        reg.IsActive = false;
                        reg.CancellationSource = LunchCancellationSource.Staff;
                        reg.CancelledAt = DateTime.UtcNow;
                        reg.CancelledByUserId = adminUserId;
                        reg.CancellationReason = string.IsNullOrWhiteSpace(request.Note)
                            ? "Đóng bán trú theo lịch nghỉ của trường"
                            : request.Note.Trim();

                        if (notifRepo != null && reg.Student?.StudentGuardians != null)
                        {
                            foreach (var sg in reg.Student.StudentGuardians)
                            {
                                var parentUserId = sg.Guardian?.UserId;
                                if (parentUserId.HasValue && notifiedKeys.Add((parentUserId.Value, reg.StudentId)))
                                {
                                    await notifRepo.AddAsync(new Notification
                                    {
                                        UserId = parentUserId.Value,
                                        Type = "LunchDayClosedBySchoolCalendar",
                                        Title = $"Nghỉ học & hủy suất ăn ngày {date:dd/MM/yyyy}",
                                        Content = $"Ngày {date:dd/MM/yyyy} được cấu hình là ngày nghỉ ({trimmedNote ?? "theo lịch nhà trường"}). Suất ăn trưa của học sinh {reg.Student.FullName} đã được hủy.",
                                        LinkUrl = "/parent/lunch",
                                        ReferenceKey = $"calendar_lunch_cancel:{date:yyyy-MM-dd}:{reg.StudentId}:{parentUserId.Value}",
                                        CreatedAt = DateTime.UtcNow,
                                        IsRead = false
                                    }, ct);
                                }
                            }
                        }
                    }
                }

                await _unitOfWork.SaveChangesAsync(ct);
            }
            else if (isWeekend)
            {
                if (lunchDay == null)
                {
                    await lunchDayRepo.AddAsync(new LunchServiceDay
                    {
                        Date = date,
                        IsServing = true,
                        CutoffTime = _options.LunchCutoff,
                        Remarks = string.IsNullOrWhiteSpace(request.Note) ? "Mở bán trú ngày học bù" : request.Note.Trim()
                    }, ct);
                }
                else
                {
                    lunchDay.IsServing = true;
                }
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }

        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.SchoolCalendarDayConfigure,
            nameof(SchoolCalendarDay),
            existing.Id.ToString(),
            oldValues,
            new { Date = date.ToString("yyyy-MM-dd"), existing.IsSchoolDay, existing.Note },
            null, // TODO: pass the client IP address; it is not recorded yet.
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<SchoolCalendarDayDto>.Ok(new SchoolCalendarDayDto
        {
            Id = existing.Id,
            SchoolYearId = existing.SchoolYearId,
            SchoolYearName = existing.SchoolYear?.YearName ?? targetYear.YearName,
            Date = existing.Date,
            IsSchoolDay = existing.IsSchoolDay,
            Note = existing.Note
        }, SystemMessages.MSG44);
    }

    public async Task<ApiResponse<List<LunchCutoffDayDto>>> GetLunchServiceDaysAsync(string? month, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<LunchServiceDay>().Query().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse($"{month}-01", out var mStart))
        {
            var mEnd = mStart.AddMonths(1);
            query = query.Where(d => d.Date >= mStart && d.Date < mEnd);
        }

        var rows = await query.OrderBy(d => d.Date).Take(60).ToListAsync(ct);
        var items = rows.Select(d => new LunchCutoffDayDto
        {
            Id = d.Id,
            Date = d.Date,
            IsServing = d.IsServing,
            CutoffTime = d.CutoffTime.ToString(@"hh\:mm"),
            IsLocked = d.IsLocked,
            Remarks = d.Remarks
        }).ToList();

        return ApiResponse<List<LunchCutoffDayDto>>.Ok(items);
    }

    public async Task<ApiResponse<LunchCutoffDayDto>> UpsertLunchCutoffAsync(int adminUserId, UpdateLunchCutoffDto request, CancellationToken ct = default)
    {
        if (request.Date == default)
        {
            return ApiResponse<LunchCutoffDayDto>.Fail(SystemMessages.MSG01, new List<string> { "Ngày cấu hình bán trú là bắt buộc." });
        }

        var minCutoff = new TimeSpan(6, 0, 0);
        var maxCutoff = new TimeSpan(11, 0, 0);
        if (!TimeSpan.TryParse(request.CutoffTime, out var cutoff) || cutoff < minCutoff || cutoff > maxCutoff)
        {
            return ApiResponse<LunchCutoffDayDto>.Fail(SystemMessages.MSG01, new List<string> { "Giờ chốt suất ăn phải có định dạng HH:mm trong khoảng 06:00 đến 11:00 (trước giờ ăn trưa)." });
        }

        var trimmedRemarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        if (trimmedRemarks != null && trimmedRemarks.Length > 250)
        {
            return ApiResponse<LunchCutoffDayDto>.Fail(SystemMessages.MSG01, new List<string> { "Ghi chú không được vượt quá 250 ký tự." });
        }

        var date = request.Date.Date;
        var existing = await _unitOfWork.Repository<LunchServiceDay>()
            .Query()
            .FirstOrDefaultAsync(d => d.Date.Date == date, ct);

        if (existing != null && existing.IsLocked)
        {
            return ApiResponse<LunchCutoffDayDto>.Fail(
                SystemMessages.MSG19,
                new List<string> { $"Ngày {date:dd-MM-yyyy} đã chốt sổ bán trú. Vui lòng mở khóa trước khi thay đổi cấu hình." });
        }

        object? oldValues = null;
        if (existing == null)
        {
            existing = new LunchServiceDay
            {
                Date = date,
                IsServing = request.IsServing,
                CutoffTime = cutoff,
                Remarks = trimmedRemarks
            };
            await _unitOfWork.Repository<LunchServiceDay>().AddAsync(existing, ct);
        }
        else
        {
            oldValues = new { existing.IsServing, CutoffTime = existing.CutoffTime.ToString(@"hh\:mm"), existing.Remarks };
            existing.IsServing = request.IsServing;
            existing.CutoffTime = cutoff;
            existing.Remarks = trimmedRemarks;
        }

        // D-D: Khi đóng ngày phục vụ (IsServing = false), tự động hủy suất đã đăng ký của ngày đó
        var autoCancelledCount = 0;
        if (!request.IsServing)
        {
            var regRepo = _unitOfWork.Repository<LunchRegistration>();
            if (regRepo != null)
            {
                var activeRegs = await regRepo.Query()
                    .Include(r => r.Student)
                    .Where(r => r.Date == date && r.IsActive)
                    .ToListAsync(ct);

                if (activeRegs.Count > 0)
                {
                    var utcNow = DateTime.UtcNow;
                    foreach (var reg in activeRegs)
                    {
                        var reasonText = !string.IsNullOrWhiteSpace(trimmedRemarks)
                            ? $"Nhà trường đóng phục vụ bán trú ngày {date:dd-MM-yyyy}: {trimmedRemarks}"
                            : $"Nhà trường đóng phục vụ bán trú ngày {date:dd-MM-yyyy}";
                        reg.IsActive = false;
                        reg.IsWaitlisted = false;
                        reg.CancellationSource = LunchCancellationSource.Staff;
                        reg.CancelledAt = utcNow;
                        reg.CancelledByUserId = adminUserId;
                        reg.CancellationReason = reasonText.Length > 300 ? reasonText[..300] : reasonText;
                    }
                    autoCancelledCount = activeRegs.Count;

                    var notifRepo = _unitOfWork.Repository<Notification>();
                    if (notifRepo != null && _unitOfWork.Students != null)
                    {
                        var guardianMap = await _unitOfWork.Students.GetGuardianUserIdsAsync(activeRegs.Select(r => r.StudentId).Distinct(), ct);
                        foreach (var reg in activeRegs)
                        {
                            if (!guardianMap.TryGetValue(reg.StudentId, out var gIds)) continue;
                            foreach (var gId in gIds.Distinct())
                            {
                                await notifRepo.AddAsync(new Notification
                                {
                                    UserId = gId,
                                    Title = "Thông báo hủy suất ăn do đóng ngày phục vụ",
                                    Content = $"Suất ăn bán trú ngày {date:dd-MM-yyyy} của học sinh {reg.Student?.FullName ?? $"#{reg.StudentId}"} đã được tự động hủy do nhà trường không phục vụ bán trú ngày này.",
                                    LinkUrl = $"/parent/lunch?studentId={reg.StudentId}&month={date:yyyy-MM}",
                                    CreatedAt = utcNow,
                                    IsRead = false
                                }, ct);
                            }
                        }
                    }
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.LunchServiceDayConfigureCutoff,
            nameof(LunchServiceDay),
            existing.Id.ToString(),
            oldValues,
            new { Date = date.ToString("yyyy-MM-dd"), existing.IsServing, CutoffTime = existing.CutoffTime.ToString(@"hh\:mm"), existing.Remarks, AutoCancelledRegistrations = autoCancelledCount },
            null, // TODO: pass the client IP address; it is not recorded yet.
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<LunchCutoffDayDto>.Ok(new LunchCutoffDayDto
        {
            Id = existing.Id,
            Date = existing.Date,
            IsServing = existing.IsServing,
            CutoffTime = existing.CutoffTime.ToString(@"hh\:mm"),
            IsLocked = existing.IsLocked,
            Remarks = existing.Remarks
        }, SystemMessages.MSG44);
    }

    // ---- GAP-05: UC-54..UC-57 ----

    public async Task<ApiResponse<RolePermissionMatrixDto>> GetRolePermissionsMatrixAsync(CancellationToken ct = default)
    {
        var roles = await _unitOfWork.Repository<Role>()
            .Query()
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        var perms = await _unitOfWork.Repository<Permission>()
            .Query()
            .AsNoTracking()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Code)
            .ToListAsync(ct);

        var rolePerms = await _unitOfWork.Repository<RolePermission>()
            .Query()
            .AsNoTracking()
            .Where(rp => rp.IsGranted)
            .ToListAsync(ct);

        var permCodeById = perms.ToDictionary(p => p.Id, p => p.Code);
        var matrix = new RolePermissionMatrixDto
        {
            Permissions = perms.Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Module = p.Module,
                Description = p.Description
            }).ToList(),
            Roles = roles.Select(r => new RolePermissionMatrixItemDto
            {
                RoleId = r.Id,
                RoleName = r.Name,
                RoleDescription = r.Description,
                GrantedPermissionCodes = rolePerms
                    .Where(rp => rp.RoleId == r.Id && permCodeById.ContainsKey(rp.PermissionId))
                    .Select(rp => permCodeById[rp.PermissionId])
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList()
            }).ToList()
        };

        return ApiResponse<RolePermissionMatrixDto>.Ok(matrix);
    }

    public async Task<ApiResponse<bool>> UpdateRolePermissionsAsync(int adminUserId, int roleId, UpdateRolePermissionsDto request, CancellationToken ct = default)
    {
        var role = await _unitOfWork.Repository<Role>().GetByIdAsync(roleId, ct);
        if (role == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG43);
        }

        var allPerms = await _unitOfWork.Repository<Permission>().GetAllAsync(ct);
        var permByCode = allPerms.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        var requestedCodes = (request.GrantedPermissionCodes ?? new List<string>())
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var code in requestedCodes)
        {
            if (!permByCode.ContainsKey(code))
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { $"Mã quyền '{code}' không tồn tại." });
            }
        }

        var rpRepo = _unitOfWork.Repository<RolePermission>();
        var existing = await rpRepo.Query().Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        var oldCodes = existing.Where(x => x.IsGranted)
            .Join(allPerms, x => x.PermissionId, p => p.Id, (_, p) => p.Code)
            .OrderBy(c => c)
            .ToList();

        var existingByPermId = existing.ToDictionary(x => x.PermissionId);
        var desiredPermIds = requestedCodes.Select(c => permByCode[c].Id).ToHashSet();

        foreach (var rp in existing)
        {
            rp.IsGranted = desiredPermIds.Contains(rp.PermissionId);
            rp.UpdatedAt = DateTime.UtcNow;
            await rpRepo.UpdateAsync(rp, ct);
        }

        foreach (var pid in desiredPermIds)
        {
            if (!existingByPermId.ContainsKey(pid))
            {
                await rpRepo.AddAsync(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = pid,
                    IsGranted = true,
                    UpdatedAt = DateTime.UtcNow
                }, ct);
            }
        }

        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.RolePermissionsUpdate,
            nameof(Role),
            role.Id.ToString(),
            new { Role = role.Name, Permissions = oldCodes },
            new { Role = role.Name, Permissions = requestedCodes.OrderBy(c => c).ToList() },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đã cập nhật ma trận phân quyền thành công.");
    }

    public async Task<ApiResponse<AdminProfileLinkingOverviewDto>> GetUserProfileLinksAsync(int targetUserId, string? search = null, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Repository<User>()
            .Query()
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        if (user == null)
        {
            return ApiResponse<AdminProfileLinkingOverviewDto>.Fail(SystemMessages.MSG43);
        }

        var linkedGuardian = await _unitOfWork.Repository<Guardian>()
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == targetUserId, ct);

        var q = search?.Trim().ToLower();

        var staffQuery = _unitOfWork.Repository<StaffProfile>()
            .Query()
            .AsNoTracking()
            .Include(sp => sp.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
        {
            staffQuery = staffQuery.Where(sp =>
                sp.StaffCode.ToLower().Contains(q) ||
                sp.User.FullName.ToLower().Contains(q) ||
                sp.Department.ToLower().Contains(q));
        }

        var staffOptions = await staffQuery
            .OrderBy(sp => sp.StaffCode)
            .Take(60)
            .Select(sp => new AdminProfileLinkOptionDto
            {
                ProfileId = sp.Id,
                ProfileType = "Staff",
                CodeOrPhone = sp.StaffCode,
                FullName = sp.User.FullName,
                SecondaryInfo = $"{sp.Position} • {sp.Department}",
                LinkedUserId = sp.UserId,
                LinkedUsername = sp.User.Username
            })
            .ToListAsync(ct);

        var guardianQuery = _unitOfWork.Repository<Guardian>()
            .Query()
            .AsNoTracking()
            .Include(g => g.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
        {
            guardianQuery = guardianQuery.Where(g =>
                g.FullName.ToLower().Contains(q) ||
                (g.PhoneNumber != null && g.PhoneNumber.Contains(q)) ||
                (g.Email != null && g.Email.ToLower().Contains(q)));
        }

        var guardianOptions = await guardianQuery
            .OrderBy(g => g.UserId == null ? 0 : 1)
            .ThenBy(g => g.FullName)
            .Take(60)
            .Select(g => new AdminProfileLinkOptionDto
            {
                ProfileId = g.Id,
                ProfileType = "Guardian",
                CodeOrPhone = g.PhoneNumber ?? g.IdentityCardNumber,
                FullName = g.FullName,
                SecondaryInfo = g.Email,
                LinkedUserId = g.UserId,
                LinkedUsername = g.User != null ? g.User.Username : null
            })
            .ToListAsync(ct);

        return ApiResponse<AdminProfileLinkingOverviewDto>.Ok(new AdminProfileLinkingOverviewDto
        {
            User = new AdminUserProfileLinkInfoDto
            {
                UserId = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
                LinkedStaffProfileId = user.StaffProfile?.Id,
                LinkedStaffLabel = user.StaffProfile != null ? $"{user.StaffProfile.StaffCode} - {user.StaffProfile.Position}" : null,
                LinkedGuardianId = linkedGuardian?.Id,
                LinkedGuardianLabel = linkedGuardian != null ? $"{linkedGuardian.FullName} ({linkedGuardian.PhoneNumber ?? linkedGuardian.Email})" : null
            },
            StaffProfiles = staffOptions,
            Guardians = guardianOptions
        });
    }

    public async Task<ApiResponse<bool>> UpdateUserProfileLinkAsync(int adminUserId, int targetUserId, UpdateUserProfileLinkDto request, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Repository<User>()
            .Query()
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        if (user == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG43);
        }

        if (string.Equals(request.ProfileType, "Guardian", StringComparison.OrdinalIgnoreCase))
        {
            var gRepo = _unitOfWork.Repository<Guardian>();
            var currentLinked = await gRepo.Query().Where(g => g.UserId == targetUserId).ToListAsync(ct);

            if (!request.ProfileId.HasValue || request.ProfileId.Value <= 0)
            {
                foreach (var oldG in currentLinked)
                {
                    oldG.UserId = null;
                    await gRepo.UpdateAsync(oldG, ct);
                }

                await _auditService.LogActionAsync(
                    adminUserId,
                    AuditActions.UserProfileLinkUpdate,
                    nameof(User),
                    targetUserId.ToString(),
                    new { ProfileType = "Guardian", OldProfileIds = currentLinked.Select(x => x.Id).ToList() },
                    new { ProfileType = "Guardian", NewProfileId = (int?)null },
                    null,
                    ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ApiResponse<bool>.Ok(true, "Đã gỡ liên kết hồ sơ phụ huynh khỏi tài khoản.");
            }

            var targetGuardian = await gRepo.GetByIdAsync(request.ProfileId.Value, ct);
            if (targetGuardian == null)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG43);
            }
            if (targetGuardian.UserId.HasValue && targetGuardian.UserId.Value != targetUserId)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Hồ sơ phụ huynh này đang liên kết với một tài khoản khác. Vui lòng gỡ liên kết cũ trước." });
            }

            foreach (var oldG in currentLinked.Where(x => x.Id != targetGuardian.Id))
            {
                oldG.UserId = null;
                await gRepo.UpdateAsync(oldG, ct);
            }

            targetGuardian.UserId = targetUserId;
            await gRepo.UpdateAsync(targetGuardian, ct);

            await _auditService.LogActionAsync(
                adminUserId,
                AuditActions.UserProfileLinkUpdate,
                nameof(User),
                targetUserId.ToString(),
                new { ProfileType = "Guardian", OldProfileIds = currentLinked.Select(x => x.Id).ToList() },
                new { ProfileType = "Guardian", NewProfileId = targetGuardian.Id },
                null,
                ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.Ok(true, $"Đã liên kết tài khoản với phụ huynh {targetGuardian.FullName}.");
        }
        else if (string.Equals(request.ProfileType, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.ProfileId.HasValue || request.ProfileId.Value <= 0)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Hồ sơ nhân sự yêu cầu gắn với một tài khoản; vui lòng chọn hồ sơ nhân sự cần gán." });
            }

            var spRepo = _unitOfWork.Repository<StaffProfile>();
            var targetStaff = await spRepo.GetByIdAsync(request.ProfileId.Value, ct);
            if (targetStaff == null)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG43);
            }

            if (user.StaffProfile != null && user.StaffProfile.Id != targetStaff.Id)
            {
                return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { $"Tài khoản này đang gắn với hồ sơ nhân sự {user.StaffProfile.StaffCode}." });
            }

            var oldOwnerId = targetStaff.UserId;
            targetStaff.UserId = targetUserId;
            await spRepo.UpdateAsync(targetStaff, ct);

            await _auditService.LogActionAsync(
                adminUserId,
                AuditActions.UserProfileLinkUpdate,
                nameof(StaffProfile),
                targetStaff.Id.ToString(),
                new { OldUserId = oldOwnerId },
                new { NewUserId = targetUserId, targetStaff.StaffCode },
                null,
                ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.Ok(true, $"Đã gắn hồ sơ nhân sự {targetStaff.StaffCode} cho tài khoản {user.Username}.");
        }

        return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Loại hồ sơ phải là Staff hoặc Guardian." });
    }

    public async Task<ApiResponse<List<ReferenceItemDto>>> GetReferenceItemsAsync(string? category = null, bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<ReferenceItem>().Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim();
            query = query.Where(x => x.Category == cat);
        }
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var rows = await query
            .OrderBy(x => x.Category)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .Select(x => new ReferenceItemDto
            {
                Id = x.Id,
                Category = x.Category,
                Code = x.Code,
                Label = x.Label,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            row.UpdatedAt = UtcDateTimeHelper.Normalize(row.UpdatedAt);
        }

        return ApiResponse<List<ReferenceItemDto>>.Ok(rows);
    }

    public async Task<ApiResponse<ReferenceItemDto>> UpsertReferenceItemAsync(int adminUserId, int? id, UpsertReferenceItemDto request, CancellationToken ct = default)
    {
        var category = request.Category?.Trim() ?? string.Empty;
        var code = request.Code?.Trim() ?? string.Empty;
        var label = request.Label?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(label))
        {
            return ApiResponse<ReferenceItemDto>.Fail(SystemMessages.MSG01, new List<string> { "Nhóm danh mục, mã và nhãn hiển thị không được để trống." });
        }
        if (category.Length > 80 || code.Length > 80 || label.Length > 200)
        {
            return ApiResponse<ReferenceItemDto>.Fail(SystemMessages.MSG01, new List<string> { "Độ dài trường danh mục vượt quá giới hạn cho phép." });
        }

        var repo = _unitOfWork.Repository<ReferenceItem>();
        ReferenceItem? entity;

        if (id.HasValue && id.Value > 0)
        {
            entity = await repo.GetByIdAsync(id.Value, ct);
            if (entity == null) return ApiResponse<ReferenceItemDto>.Fail(SystemMessages.MSG43);

            var duplicate = await repo.Query().AnyAsync(x => x.Id != entity.Id && x.Category == category && x.Code == code, ct);
            if (duplicate)
            {
                return ApiResponse<ReferenceItemDto>.Fail(SystemMessages.MSG09, new List<string> { $"Mã '{code}' đã tồn tại trong danh mục '{category}'." });
            }

            entity.Category = category;
            entity.Code = code;
            entity.Label = label;
            entity.SortOrder = request.SortOrder;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
            await repo.UpdateAsync(entity, ct);
        }
        else
        {
            var duplicate = await repo.Query().AnyAsync(x => x.Category == category && x.Code == code, ct);
            if (duplicate)
            {
                return ApiResponse<ReferenceItemDto>.Fail(SystemMessages.MSG09, new List<string> { $"Mã '{code}' đã tồn tại trong danh mục '{category}'." });
            }

            entity = new ReferenceItem
            {
                Category = category,
                Code = code,
                Label = label,
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                UpdatedAt = DateTime.UtcNow
            };
            await repo.AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.ReferenceItemSave,
            nameof(ReferenceItem),
            entity.Id.ToString(),
            null,
            new { entity.Category, entity.Code, entity.Label, entity.IsActive },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<ReferenceItemDto>.Ok(new ReferenceItemDto
        {
            Id = entity.Id,
            Category = entity.Category,
            Code = entity.Code,
            Label = entity.Label,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            UpdatedAt = UtcDateTimeHelper.Normalize(entity.UpdatedAt)
        }, "Đã lưu mục danh mục tham chiếu.");
    }

    public async Task<ApiResponse<List<NotificationTemplateDto>>> GetNotificationTemplatesAsync(CancellationToken ct = default)
    {
        var rows = await _unitOfWork.Repository<NotificationTemplate>()
            .Query()
            .AsNoTracking()
            .OrderBy(t => t.Code)
            .Select(t => new NotificationTemplateDto
            {
                Id = t.Id,
                Code = t.Code,
                TitleTemplate = t.TitleTemplate,
                BodyTemplate = t.BodyTemplate,
                IsActive = t.IsActive,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            row.UpdatedAt = UtcDateTimeHelper.Normalize(row.UpdatedAt);
        }

        return ApiResponse<List<NotificationTemplateDto>>.Ok(rows);
    }

    public async Task<ApiResponse<NotificationTemplateDto>> UpdateNotificationTemplateAsync(int adminUserId, int id, UpdateNotificationTemplateDto request, CancellationToken ct = default)
    {
        var title = request.TitleTemplate?.Trim() ?? string.Empty;
        var body = request.BodyTemplate?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body))
        {
            return ApiResponse<NotificationTemplateDto>.Fail(SystemMessages.MSG01, new List<string> { "Tiêu đề mẫu và nội dung mẫu thông báo không được để trống." });
        }
        if (title.Length > 200 || body.Length > 1000)
        {
            return ApiResponse<NotificationTemplateDto>.Fail(SystemMessages.MSG01, new List<string> { "Độ dài mẫu thông báo vượt quá giới hạn cho phép." });
        }

        var repo = _unitOfWork.Repository<NotificationTemplate>();
        var entity = await repo.GetByIdAsync(id, ct);
        if (entity == null)
        {
            return ApiResponse<NotificationTemplateDto>.Fail(SystemMessages.MSG43);
        }

        var oldValues = new { entity.TitleTemplate, entity.BodyTemplate, entity.IsActive };
        entity.TitleTemplate = title;
        entity.BodyTemplate = body;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await repo.UpdateAsync(entity, ct);
        await _auditService.LogActionAsync(
            adminUserId,
            AuditActions.NotificationTemplateSave,
            nameof(NotificationTemplate),
            entity.Id.ToString(),
            oldValues,
            new { entity.Code, entity.TitleTemplate, entity.BodyTemplate, entity.IsActive },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<NotificationTemplateDto>.Ok(new NotificationTemplateDto
        {
            Id = entity.Id,
            Code = entity.Code,
            TitleTemplate = entity.TitleTemplate,
            BodyTemplate = entity.BodyTemplate,
            IsActive = entity.IsActive,
            UpdatedAt = UtcDateTimeHelper.Normalize(entity.UpdatedAt)
        }, "Đã cập nhật mẫu thông báo hệ thống.");
    }

    private static AdminUserDto MapToUserDto(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        FullName = u.FullName,
        PhoneNumber = u.PhoneNumber,
        IsActive = u.Status == AccountStatus.Active,
        Status = u.Status.ToString(),
        CanDelete = (u.Status == AccountStatus.PendingActivation || (u.Status == AccountStatus.Deactivated && string.IsNullOrEmpty(u.PasswordHash)))
            && !u.UserRoles.Any(ur => ur.Role != null && ur.Role.Name.Equals(UserRoles.Admin.ToString(), StringComparison.OrdinalIgnoreCase)),
        CreatedAt = UtcDateTimeHelper.Normalize(u.CreatedAt),
        Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
        StaffCode = u.StaffProfile?.StaffCode,
        Position = u.StaffProfile?.Position,
        Department = u.StaffProfile?.Department
    };
}
