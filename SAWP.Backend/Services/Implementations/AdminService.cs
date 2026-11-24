using Core.Enums;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class AdminService : IAdminService
{
    private static readonly string[] StaffRoleNames = { "Teacher", "OfficeStaff", "ServiceStaff", "SchoolBoard" };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public AdminService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<ApiResponse<PaginatedResult<AdminUserDto>>> GetUsersPagedAsync(
        int pageIndex, int pageSize, string? role, bool? isActive, string? search, CancellationToken ct = default)
    {
        var page = Math.Max(1, pageIndex);
        var size = Math.Clamp(pageSize, 1, 100);

        var query = _unitOfWork.Repository<User>()
            .Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.StaffProfile)
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

        query = query.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id);

        var total = await query.CountAsync(ct);
        var users = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        var items = users.Select(MapToUserDto).ToList();
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
        var fullName = request.FullName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName))
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập đầy đủ Tên đăng nhập, Email và Họ tên." });
        }

        if (!PasswordPolicy.IsValid(request.Password))
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG04, new List<string> { "Mật khẩu phải có ít nhất 8 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt." });
        }

        if (request.Roles == null || request.Roles.Count == 0)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn ít nhất một vai trò." });
        }

        var exists = await _unitOfWork.Repository<User>()
            .Query()
            .AnyAsync(u => u.Username.ToLower() == username.ToLower() || u.Email.ToLower() == email.ToLower(), ct);

        if (exists)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Tên đăng nhập hoặc Email đã tồn tại trong hệ thống." });
        }

        var requestedRoleNames = request.Roles.Select(r => r.Trim().ToLower()).Distinct().ToList();
        var dbRoles = await _unitOfWork.Repository<Role>()
            .Query()
            .Where(r => requestedRoleNames.Contains(r.Name.ToLower()))
            .ToListAsync(ct);

        if (dbRoles.Count == 0)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Vai trò được chọn không hợp lệ." });
        }

        var user = new User
        {
            Username = username,
            Email = email,
            FullName = fullName,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Status = AccountStatus.Active, // TODO SSAWP-98: start in Pending Activation with an activation link (BR-63)
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
            new { user.Username, user.FullName, user.Email, Roles = dbRoles.Select(r => r.Name) },
            null, // TODO: pass the client IP address; it is not recorded yet.
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<AdminUserDto>.Ok(MapToUserDto(user), SystemMessages.MSG38);
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
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        if (user == null)
        {
            return ApiResponse<AdminUserDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy tài khoản người dùng." });
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

        var oldStatus = user.Status;
        user.Status = isActive ? AccountStatus.Active : AccountStatus.Deactivated;
        user.UpdatedAt = DateTime.UtcNow;

        await _auditService.LogActionAsync(
            adminUserId,
            isActive ? AuditActions.UserActivate : AuditActions.UserDeactivate,
            nameof(User),
            user.Id.ToString(),
            new { Status = oldStatus.ToString() },
            new { Status = user.Status.ToString() },
            null, // TODO: pass the client IP address; it is not recorded yet.
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, isActive ? SystemMessages.MSG08 : SystemMessages.MSG39);
    }

    public async Task<ApiResponse<bool>> ResetUserPasswordAsync(int adminUserId, int targetUserId, string newPassword, CancellationToken ct = default)
    {
        if (!PasswordPolicy.IsValid(newPassword))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG04, new List<string> { "Mật khẩu mới chưa đáp ứng yêu cầu bảo mật (tối thiểu 8 ký tự, gồm hoa, thường, số, ký tự đặc biệt)." });
        }

        var user = await _unitOfWork.Repository<User>().GetByIdAsync(targetUserId, ct);
        if (user == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy tài khoản người dùng." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
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
        if (request.Date == default)
        {
            return ApiResponse<SchoolCalendarDayDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Ngày cấu hình lịch học là bắt buộc." });
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

    private static AdminUserDto MapToUserDto(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        FullName = u.FullName,
        PhoneNumber = u.PhoneNumber,
        IsActive = u.Status == AccountStatus.Active,
        Status = u.Status.ToString(),
        CreatedAt = u.CreatedAt,
        Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
        StaffCode = u.StaffProfile?.StaffCode,
        Position = u.StaffProfile?.Position,
        Department = u.StaffProfile?.Department
    };
}
