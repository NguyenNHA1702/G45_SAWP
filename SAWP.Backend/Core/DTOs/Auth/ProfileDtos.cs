namespace Core.DTOs;

public class UserProfileDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Roles { get; set; } = new();

    public StaffProfileDetailDto? StaffProfile { get; set; }
    public GuardianProfileDetailDto? GuardianProfile { get; set; }
}

public class StaffProfileDetailDto
{
    public string StaffCode { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public bool IsHomeroomTeacher { get; set; }
    public string? HomeroomClassName { get; set; }
}

public class GuardianProfileDetailDto
{
    public string IdentityCardNumber { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public string? Address { get; set; }
    public List<LinkedChildSummaryDto> Children { get; set; } = new();
}

public class UpdateProfileRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Occupation { get; set; }
}
