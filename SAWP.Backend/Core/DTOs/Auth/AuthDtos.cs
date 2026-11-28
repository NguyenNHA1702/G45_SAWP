namespace Core.DTOs;

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public DateTime ExpiresAt { get; set; }

    /// <summary>True after an administrator-assisted reset: the client must open Change Password first (UC-60 A1).</summary>
    public bool MustChangePassword { get; set; }
}

/// <summary>UC-66 step 2: request a recovery (or activation) link.</summary>
public class ForgotPasswordRequestDto
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>UC-66 step 6: check the link when its page opens; does not use it.</summary>
public class PasswordLinkCheckRequestDto
{
    public string Token { get; set; } = string.Empty;
}

public class PasswordLinkCheckResultDto
{
    /// <summary>"Activation" or "PasswordRecovery".</summary>
    public string Purpose { get; set; } = string.Empty;
}

/// <summary>UC-66 steps 7-8: set a new password with the link.</summary>
public class SetPasswordWithLinkRequestDto
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class EmailChangeLinkTokenDto
{
    public string Token { get; set; } = string.Empty;
}

public class EmailChangeLinkCheckResultDto
{
    public string CurrentEmail { get; set; } = string.Empty;
    public string NewEmail { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
