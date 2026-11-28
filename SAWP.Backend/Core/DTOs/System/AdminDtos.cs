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
    // No password: the account starts Pending Activation and the holder sets it through the activation link (BR-63).
    public List<string> Roles { get; set; } = new();
    public string? StaffCode { get; set; }
    public string? Position { get; set; }
    public string? Department { get; set; }
    public string? HomeroomClassName { get; set; }
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
