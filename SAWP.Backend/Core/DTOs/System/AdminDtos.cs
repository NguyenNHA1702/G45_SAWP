namespace Core.DTOs;

public class RoleOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AdminUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Account state: PendingActivation, Active or Deactivated (BR-63).</summary>
    public string Status { get; set; } = string.Empty;
    public bool CanDelete { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
    public string? StaffCode { get; set; }
    public string? Position { get; set; }
    public string? Department { get; set; }
    public string? HomeroomClassName { get; set; }
}

public class CreateUserRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    // No password: the account starts Disabled (unless EnableNow = true -> Pending Activation + sends activation link).
    public List<string> Roles { get; set; } = new();
    public bool EnableNow { get; set; } = false;
    public string? StaffCode { get; set; }
    public string? Position { get; set; }
    public string? Department { get; set; }
    public string? HomeroomClassName { get; set; }
}

public class AdminChangeEmailRequestDto
{
    public string NewEmail { get; set; } = string.Empty;
}

public class UpdateUserRolesRequestDto
{
    public List<string> Roles { get; set; } = new();
}

public class UpdateUserStatusRequestDto
{
    public bool IsActive { get; set; }
}

public class ResetUserPasswordRequestDto
{
    public string NewPassword { get; set; } = string.Empty;
}

public class SchoolCalendarDayDto
{
    public int Id { get; set; }
    public int SchoolYearId { get; set; }
    public string? SchoolYearName { get; set; }
    public DateTime Date { get; set; }
    public bool IsSchoolDay { get; set; }
    public string? Note { get; set; }
}

public class UpdateCalendarDayDto
{
    public int? SchoolYearId { get; set; }
    public DateTime Date { get; set; }
    public bool IsSchoolDay { get; set; }
    public string? Note { get; set; }
}

public class LunchCutoffDayDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public bool IsServing { get; set; }
    public string CutoffTime { get; set; } = "08:15";
    public bool IsLocked { get; set; }
    public string? Remarks { get; set; }
}

public class UpdateLunchCutoffDto
{
    public DateTime Date { get; set; }
    public bool IsServing { get; set; } = true;
    public string CutoffTime { get; set; } = "08:15";
    public string? Remarks { get; set; }
}

// ---- GAP-05: Admin UC-54..UC-57 ----

public class PermissionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class RolePermissionMatrixItemDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? RoleDescription { get; set; }
    public List<string> GrantedPermissionCodes { get; set; } = new();
}

public class RolePermissionMatrixDto
{
    public List<PermissionDto> Permissions { get; set; } = new();
    public List<RolePermissionMatrixItemDto> Roles { get; set; } = new();
}

public class UpdateRolePermissionsDto
{
    public List<string> GrantedPermissionCodes { get; set; } = new();
}

public class AdminProfileLinkOptionDto
{
    public int ProfileId { get; set; }
    public string ProfileType { get; set; } = string.Empty; // Staff | Guardian
    public string CodeOrPhone { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? SecondaryInfo { get; set; }
    public int? LinkedUserId { get; set; }
    public string? LinkedUsername { get; set; }
}

public class AdminUserProfileLinkInfoDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public int? LinkedStaffProfileId { get; set; }
    public string? LinkedStaffLabel { get; set; }
    public int? LinkedGuardianId { get; set; }
    public string? LinkedGuardianLabel { get; set; }
}

public class AdminProfileLinkingOverviewDto
{
    public AdminUserProfileLinkInfoDto? User { get; set; }
    public List<AdminProfileLinkOptionDto> StaffProfiles { get; set; } = new();
    public List<AdminProfileLinkOptionDto> Guardians { get; set; } = new();
}

public class UpdateUserProfileLinkDto
{
    public string ProfileType { get; set; } = "Staff"; // Staff | Guardian
    public int? ProfileId { get; set; } // null = unlink
}

public class ReferenceItemDto
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpsertReferenceItemDto
{
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class NotificationTemplateDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateNotificationTemplateDto
{
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
