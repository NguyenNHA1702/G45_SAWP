using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using System.Net.Mail;
using Core.Configuration;
using Infrastructure.ExternalServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Services.Interfaces;

namespace Services.Implementations;

public class AuthService : IAuthService
{
    /// <summary>Claim that carries <see cref="User.SessionVersion"/> in every login token.</summary>
    public const string SessionVersionClaim = "sv";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IAccountLinkService _accountLinks;
    private readonly IBackgroundWorkQueue _backgroundWork;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly IMemoryCache _cache;
    private readonly AccountLinkOptions _linkOptions;

    public AuthService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        IAccountLinkService accountLinks,
        IBackgroundWorkQueue backgroundWork,
        IEmailService emailService,
        IAuditService auditService,
        IMemoryCache cache,
        IOptions<AccountLinkOptions> linkOptions)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
        _accountLinks = accountLinks;
        _backgroundWork = backgroundWork;
        _emailService = emailService;
        _auditService = auditService;
        _cache = cache;
        _linkOptions = linkOptions.Value;
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
        if (!VerifyPassword(user, request.Password))
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG02);
        }

        // BR-01, BR-63: only Active accounts sign in (not Pending Activation or Deactivated)
        if (user.Status != AccountStatus.Active)
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG03);
        }

        var responseData = BuildLoginResponse(user);
        return ApiResponse<LoginResponseDto>.Ok(responseData, SystemMessages.MSG05);
    }

    public async Task<ApiResponse<LoginResponseDto>> ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG01);
        }

        // BR-02: Validate new password policy (at least 8 chars, uppercase, lowercase, numeric, special char)
        if (!PasswordPolicy.IsValid(request.NewPassword))
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG04, new List<string>
            {
                "Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt."
            });
        }

        var user = await _unitOfWork.Users.GetByIdWithRolesAsync(userId, cancellationToken);
        if (user == null || user.Status != AccountStatus.Active)
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG03);
        }

        if (!VerifyPassword(user, request.CurrentPassword))
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG02);
        }

        // A temporary password must really be replaced (BR-02, UC-60 A1)
        if (request.NewPassword == request.CurrentPassword)
        {
            return ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG04, new List<string> { "Mật khẩu mới phải khác mật khẩu hiện tại." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        // BR-03: a password change ends the other sessions; this session continues with the new token below
        user.SessionVersion++;
        user.UpdatedAt = DateTime.UtcNow;

        await _auditService.LogActionAsync(userId, AuditActions.UserChangePassword, nameof(User), user.Id.ToString(),
            null, new { ChangedAt = user.UpdatedAt }, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<LoginResponseDto>.Ok(BuildLoginResponse(user), SystemMessages.MSG46);
    }

    public async Task<ApiResponse<bool>> RequestPasswordLinkAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        if (!IsValidEmail(email))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01); // E1
        }

        // E5: limit per email address entered, whether or not it is registered
        if (!TryCountLinkRequest(email))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG58);
        }

        // E2: while the Email Service is down every request gets MSG59, so the answer never depends on the address
        if (!await _emailService.IsAvailableAsync(cancellationToken))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG59);
        }

        var normalized = email.ToLowerInvariant();
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, cancellationToken);

        // A4 / BR-04: no link for System Administrator accounts or accounts that cannot sign in; same answer (MSG56).
        // The link is created and emailed after the response: doing it here would make the answer slower for a
        // registered address, which tells a caller that it is registered. A failed send is only logged (E2).
        if (user != null && !IsAdministrator(user)
            && (user.Status == AccountStatus.PendingActivation || user.Status == AccountStatus.Active))
        {
            var userId = user.Id;
            _backgroundWork.Enqueue($"password link for user {userId}", (services, ct) => SendPasswordLinkAsync(services, userId, ct));
        }

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG56);
    }

    /// <summary>Background part of UC-66 step 4: reloads the account (it may have changed) and emails the right link.</summary>
    public static async Task SendPasswordLinkAsync(IServiceProvider services, int userId, CancellationToken ct)
    {
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var links = services.GetRequiredService<IAccountLinkService>();
        var user = await unitOfWork.Users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null || IsAdministrator(user))
        {
            return;
        }

        if (user.Status == AccountStatus.PendingActivation)
        {
            await links.SendActivationLinkAsync(user, ct); // UC-66 A3, BR-63
        }
        else if (user.Status == AccountStatus.Active)
        {
            await links.SendRecoveryLinkAsync(user, ct);
        }
    }

    public async Task<ApiResponse<PasswordLinkCheckResultDto>> CheckPasswordLinkAsync(PasswordLinkCheckRequestDto request, CancellationToken cancellationToken = default)
    {
        var token = await FindPasswordLinkAsync(request.Token, cancellationToken);
        if (token == null)
        {
            return ApiResponse<PasswordLinkCheckResultDto>.Fail(SystemMessages.MSG57); // E3
        }

        // Opening the page does not use the link (UC-66 step 6)
        return ApiResponse<PasswordLinkCheckResultDto>.Ok(new PasswordLinkCheckResultDto { Purpose = token.Purpose.ToString() });
    }

    public async Task<ApiResponse<bool>> SetPasswordWithLinkAsync(SetPasswordWithLinkRequestDto request, CancellationToken cancellationToken = default)
    {
        var token = await FindPasswordLinkAsync(request.Token, cancellationToken);
        if (token == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG57); // E3
        }

        if (request.NewPassword != request.ConfirmPassword || !PasswordPolicy.IsValid(request.NewPassword))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG04); // E4
        }

        var user = token.User;
        var now = DateTime.UtcNow;
        var activating = token.Purpose == VerificationTokenPurpose.Activation;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.SessionVersion++; // step 9: all active sessions end
        user.EmailVerifiedAt ??= now; // the link reached this address (BR-64)
        if (activating)
        {
            user.Status = AccountStatus.Active; // A3, BR-63
        }
        user.UpdatedAt = now;

        // The link is used; any other open activation or recovery link of the account ends too
        token.UsedAt = now;
        var others = await _unitOfWork.Repository<VerificationToken>().Query()
            .Where(t => t.UserId == user.Id && t.Id != token.Id && t.UsedAt == null && t.ExpiresAt > now
                        && (t.Purpose == VerificationTokenPurpose.Activation || t.Purpose == VerificationTokenPurpose.PasswordRecovery))
            .ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            other.ExpiresAt = now;
        }

        await _auditService.LogActionAsync(user.Id,
            activating ? AuditActions.UserActivateByLink : AuditActions.UserRecoverPassword,
            nameof(User), user.Id.ToString(), null, new { At = now }, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG46); // step 10
    }

    /// <summary>An activation or recovery link that may still be used: unused, unexpired, and for an account in the matching state.</summary>
    private async Task<VerificationToken?> FindPasswordLinkAsync(string rawToken, CancellationToken ct)
    {
        var token = await _accountLinks.FindUsableAsync(rawToken, ct);
        if (token == null || IsAdministrator(token.User))
        {
            return null;
        }

        return token.Purpose switch
        {
            VerificationTokenPurpose.Activation when token.User.Status == AccountStatus.PendingActivation => token,
            VerificationTokenPurpose.PasswordRecovery when token.User.Status == AccountStatus.Active => token,
            _ => null
        };
    }

    private sealed class LinkRequestCounter
    {
        public int Count;
    }

    /// <summary>Counts a link request for the address entered; false once the limit for the current window is reached.</summary>
    private bool TryCountLinkRequest(string email)
    {
        var key = "password-link-requests:" + email.ToLowerInvariant();
        // The window starts at the first request; later requests do not extend it
        var counter = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_linkOptions.RequestWindowMinutes);
            return new LinkRequestCounter();
        })!;
        return Interlocked.Increment(ref counter.Count) <= _linkOptions.MaxRequestsPerWindow;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256)
        {
            return false;
        }
        return MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;
    }

    private static bool IsAdministrator(User user) =>
        user.UserRoles.Any(ur => ur.Role != null && ur.Role.Name.Equals(UserRoles.Admin.ToString(), StringComparison.OrdinalIgnoreCase));

    private LoginResponseDto BuildLoginResponse(User user)
    {
        var roles = user.UserRoles.Where(ur => ur.Role != null).Select(ur => ur.Role.Name).ToList();
        if (roles.Count == 0)
        {
            roles.Add("Parent"); // Default role if not explicitly assigned
        }

        int expiresHours = int.TryParse(_configuration["Jwt:ExpiresHours"], out int h) ? h : 8;
        var expiresAt = DateTime.UtcNow.AddHours(expiresHours);
        return new LoginResponseDto
        {
            Token = GenerateJwtToken(user, roles, expiresAt),
            Username = user.Username,
            FullName = user.FullName,
            Roles = roles,
            ExpiresAt = expiresAt,
            MustChangePassword = user.MustChangePassword
        };
    }

    // Checks a password against the stored BCrypt hash. A stored value that is not a BCrypt hash never
    // matches: it is a data error (e.g. a password written to the database as plain text), so it is logged.
    private bool VerifyPassword(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return false; // no usable password yet (Pending Activation, BR-63)
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch (Exception ex) when (ex is SaltParseException or ArgumentException)
        {
            _logger.LogWarning("User {UserId} has a stored password that is not a BCrypt hash; the password was rejected.", user.Id);
            return false;
        }
    }

    private string GenerateJwtToken(User user, List<string> roles, DateTime expiresAt)
    {
        var secretKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
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
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(SessionVersionClaim, user.SessionVersion.ToString())
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

    public async Task<ApiResponse<UserProfileDto>> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.StaffProfile)!.ThenInclude(sp => sp!.HomeroomClasses)
            .Include(u => u.Guardian)!.ThenInclude(g => g!.StudentGuardians).ThenInclude(sg => sg.Student).ThenInclude(s => s.ClassAssignments).ThenInclude(ca => ca.Class)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
        {
            return ApiResponse<UserProfileDto>.Fail(SystemMessages.MSG07);
        }

        var dto = new UserProfileDto
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.Status == AccountStatus.Active,
            CreatedAt = user.CreatedAt,
            Roles = user.UserRoles.Where(ur => ur.Role != null).Select(ur => ur.Role.Name).ToList()
        };

        if (user.StaffProfile != null)
        {
            var homeroomClass = user.StaffProfile.HomeroomClasses.FirstOrDefault(c => c.Status == "Active");
            dto.StaffProfile = new StaffProfileDetailDto
            {
                StaffCode = user.StaffProfile.StaffCode,
                Department = user.StaffProfile.Department,
                Position = user.StaffProfile.Position,
                IsHomeroomTeacher = user.StaffProfile.IsHomeroomTeacher,
                HomeroomClassName = homeroomClass?.ClassName
            };
        }

        if (user.Guardian != null)
        {
            dto.GuardianProfile = new GuardianProfileDetailDto
            {
                IdentityCardNumber = user.Guardian.IdentityCardNumber,
                Occupation = user.Guardian.Occupation,
                Address = user.Guardian.Address,
                Children = user.Guardian.StudentGuardians
                    .Where(sg => sg.Student != null)
                    .Select(sg =>
                    {
                        var activeClass = sg.Student.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class;
                        return new LinkedChildSummaryDto
                        {
                            StudentId = sg.Student.Id,
                            StudentCode = sg.Student.StudentCode,
                            FullName = sg.Student.FullName,
                            Gender = sg.Student.Gender,
                            DateOfBirth = sg.Student.DateOfBirth,
                            AvatarUrl = sg.Student.AvatarUrl,
                            Status = sg.Student.Status,
                            CurrentClassId = activeClass?.Id,
                            CurrentClassName = activeClass?.ClassName,
                            Grade = activeClass?.Grade,
                            IsPlacementPublished = activeClass?.IsPlacementPublished ?? false,
                            Relationship = sg.Relationship.ToVietnamese(),
                            IsPrimaryContact = sg.IsPrimaryContact
                        };
                    }).ToList()
            };
        }

        return ApiResponse<UserProfileDto>.Ok(dto);
    }

    public async Task<ApiResponse<UserProfileDto>> UpdateProfileAsync(int userId, UpdateProfileRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return ApiResponse<UserProfileDto>.Fail(SystemMessages.MSG01);
        }

        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Guardian)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
        {
            return ApiResponse<UserProfileDto>.Fail(SystemMessages.MSG07);
        }

        user.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            user.PhoneNumber = request.PhoneNumber.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var newEmail = request.Email.Trim();
            // Login emails are unique (BR-64). TODO SSAWP-154: users no longer change their own email (BR-69).
            var emailTaken = await _unitOfWork.Users.Query()
                .AnyAsync(u => u.Id != user.Id && u.Email.ToLower() == newEmail.ToLower(), ct);
            if (emailTaken)
            {
                return ApiResponse<UserProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Email đã được sử dụng bởi một tài khoản khác." });
            }
            user.Email = newEmail;
            if (user.Guardian != null) user.Guardian.Email = newEmail; // only an address the Parent typed, never a placeholder
        }
        user.UpdatedAt = DateTime.UtcNow;

        if (user.Guardian != null)
        {
            // Keep the linked guardian record's contact details in step with the Parent's own profile, as before
            // they moved to the guardian record. TODO SSAWP-154: decide whether Parents may still edit them.
            user.Guardian.FullName = user.FullName;
            user.Guardian.PhoneNumber = user.PhoneNumber;
            if (request.Address != null) user.Guardian.Address = request.Address.Trim();
            if (request.Occupation != null) user.Guardian.Occupation = request.Occupation.Trim();
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var freshProfile = await GetProfileAsync(userId, ct);
        if (!freshProfile.Success || freshProfile.Data == null)
        {
            return freshProfile;
        }

        return ApiResponse<UserProfileDto>.Ok(freshProfile.Data, SystemMessages.MSG47);
    }
}
