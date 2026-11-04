using Core.DTOs;

namespace Services.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserProfileDto>> GetProfileAsync(int userId, CancellationToken ct = default);
    Task<ApiResponse<UserProfileDto>> UpdateProfileAsync(int userId, UpdateProfileRequestDto request, CancellationToken ct = default);
}
