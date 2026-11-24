using Core.DTOs;

namespace Services.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    /// <summary>Changes the password; returns a new token because the change ends the other sessions (BR-03).</summary>
    Task<ApiResponse<LoginResponseDto>> ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>UC-66 steps 2-5: email a recovery link, or an activation link for a Pending Activation account.</summary>
    Task<ApiResponse<bool>> RequestPasswordLinkAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>UC-66 step 6: check a link when its page opens (does not use it).</summary>
    Task<ApiResponse<PasswordLinkCheckResultDto>> CheckPasswordLinkAsync(PasswordLinkCheckRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>UC-66 steps 7-10 and A3: set the password with the link.</summary>
    Task<ApiResponse<bool>> SetPasswordWithLinkAsync(SetPasswordWithLinkRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserProfileDto>> GetProfileAsync(int userId, CancellationToken ct = default);
    Task<ApiResponse<UserProfileDto>> UpdateProfileAsync(int userId, UpdateProfileRequestDto request, CancellationToken ct = default);
}
