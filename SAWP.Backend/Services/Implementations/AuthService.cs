using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using BCrypt.Net;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Services.Interfaces;

namespace Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public AuthService(IUnitOfWork unitOfWork, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG01);
        }

        var user = await _unitOfWork.Users.GetByUsernameOrEmailWithRolesAsync(request.Username, cancellationToken);
        if (user == null)
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG02);
        }

        // BR-01: Verify password using BCrypt
        bool passwordValid = false;
        try
        {
            passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch
        {
            // Fallback for plain text seeded in early dev
            passwordValid = (user.PasswordHash == request.Password);
        }

        if (!passwordValid)
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG02);
        }

        // BR-03, BR-48: Check if account is active
        if (!user.IsActive)
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG03);
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        if (roles.Count == 0)
        {
            roles.Add("Parent"); // Default role if not explicitly assigned
        }

        int expiresHours = int.TryParse(_configuration["Jwt:ExpiresHours"], out int h) ? h : 8;
        var expiresAt = DateTime.UtcNow.AddHours(expiresHours);

        var token = GenerateJwtToken(user, roles, expiresAt);

        var responseData = new LoginResponseDto
        {
            Token = token,
            Username = user.Username,
            FullName = user.FullName,
            Roles = roles,
            ExpiresAt = expiresAt
        };

        return ApiResponse<LoginResponseDto>.Ok(responseData, SystemMessages.MSG05);
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01);
        }

        // BR-02: Validate new password policy (at least 8 chars, uppercase, lowercase, numeric, special char)
        if (!ValidatePasswordPolicy(request.NewPassword))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG04, new List<string>
            {
                "Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt."
            });
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG03);
        }

        // Verify current password
        bool currentPasswordValid = false;
        try
        {
            currentPasswordValid = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash);
        }
        catch
        {
            currentPasswordValid = (user.PasswordHash == request.CurrentPassword);
        }

        if (!currentPasswordValid)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG02);
        }

        // Hash new password with BCrypt
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG46);
    }

    private string GenerateJwtToken(User user, List<string> roles, DateTime expiresAt)
    {
        var secretKey = _configuration["Jwt:Key"] ?? "SAWP_SUPER_SECRET_SECURITY_KEY_2026_PRIMARY_SCHOOL_PLATFORM!";
        var issuer = _configuration["Jwt:Issuer"] ?? "SAWP.Backend";
        var audience = _configuration["Jwt:Audience"] ?? "SAWP.Client";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("id", user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("fullName", user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.StaffProfile != null)
        {
            claims.Add(new Claim("staffId", user.StaffProfile.Id.ToString()));
            claims.Add(new Claim("staffCode", user.StaffProfile.StaffCode));
        }

        if (user.Guardian != null)
        {
            claims.Add(new Claim("guardianId", user.Guardian.Id.ToString()));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static bool ValidatePasswordPolicy(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return false;
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]");
        return hasUpper && hasLower && hasDigit && hasSpecial;
    }
}
