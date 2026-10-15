using Core.DTOs;
using Core.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<LoginResponseDto>.Ok(new LoginResponseDto
        {
            Token = "skeleton-token",
            Username = request.Username,
            FullName = "Administrator",
            Roles = new List<string> { "Admin" },
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        });
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<bool>.Ok(true);
    }
}
