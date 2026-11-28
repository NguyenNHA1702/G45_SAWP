using API.Extensions;
using Core.Constants;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        if (!result.Success)
        {
            if (result.Code == SystemMessages.MSG02.Code || result.Code == SystemMessages.MSG03.Code)
            {
                return Unauthorized(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<LoginResponseDto>.Fail(SystemMessages.MSG07));
        }

        // A wrong current password is a 400, not a 401: the client must not treat it as an ended session.
        // On success the response carries a new token, because the change ends the other sessions (BR-03).
        var result = await _authService.ChangePasswordAsync(userId.Value, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>UC-66 steps 2-5: request a recovery (or activation) link. Always MSG56 unless E1/E2/E5 applies.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken)
        => ToActionResult(await _authService.RequestPasswordLinkAsync(request, cancellationToken));

    /// <summary>UC-66 step 6: check a link when its page opens; the link is not used. MSG57 when unusable.</summary>
    [HttpPost("password-link/check")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckPasswordLink([FromBody] PasswordLinkCheckRequestDto request, CancellationToken cancellationToken)
        => ToActionResult(await _authService.CheckPasswordLinkAsync(request, cancellationToken));

    /// <summary>UC-66 steps 7-10, A3: set the password with an activation or recovery link.</summary>
    [HttpPost("password-link/set-password")]
    [AllowAnonymous]
    public async Task<IActionResult> SetPasswordWithLink([FromBody] SetPasswordWithLinkRequestDto request, CancellationToken cancellationToken)
        => ToActionResult(await _authService.SetPasswordWithLinkAsync(request, cancellationToken));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) return Unauthorized(ApiResponse<bool>.Fail(SystemMessages.MSG07));
        return ToActionResult(await _authService.LogoutAsync(userId.Value, cancellationToken));
    }

    [HttpPost("email-change-link/check")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckEmailChangeLink([FromBody] EmailChangeLinkTokenDto request, CancellationToken cancellationToken)
        => ToActionResult(await _authService.CheckEmailChangeLinkAsync(request.Token, cancellationToken));

    [HttpPost("confirm-email-change")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmailChange([FromBody] EmailChangeLinkTokenDto request, CancellationToken cancellationToken)
        => ToActionResult(await _authService.ConfirmEmailChangeAsync(request.Token, cancellationToken));

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<object>.Fail(SystemMessages.MSG07));
        }

        var roles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        var userInfo = new
        {
            UserId = userId.Value,
            Username = User.Identity?.Name ?? string.Empty,
            FullName = User.FindFirst("fullName")?.Value ?? string.Empty,
            Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty,
            Roles = roles
        };

        return Ok(ApiResponse<object>.Ok(userInfo));
    }

    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<UserProfileDto>.Fail(SystemMessages.MSG07));
        }

        var result = await _authService.GetProfileAsync(userId.Value, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<UserProfileDto>.Fail(SystemMessages.MSG07));
        }

        var result = await _authService.UpdateProfileAsync(userId.Value, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
