using Core.Constants;
using Core.DTOs;

namespace API.Middlewares;

/// <summary>
/// After an administrator-assisted reset (UC-60 A1, BR-02) the temporary password must be changed before anything
/// else: until then only the requests below are allowed. Other requests get 403 with the header
/// <c>X-Password-Change-Required: true</c>, which tells the web client to open Change Password.
/// </summary>
public class PasswordChangeGate
{
    public const string ItemKey = "MustChangePassword";
    public const string HeaderName = "X-Password-Change-Required";

    private static readonly (string Method, string Path)[] Allowed =
    {
        ("POST", "/api/auth/change-password"),
        ("GET", "/api/auth/me"),
        ("GET", "/api/auth/profile"),
    };

    private readonly RequestDelegate _next;

    public PasswordChangeGate(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var flag) && flag is true && !IsAllowed(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers[HeaderName] = "true";
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(SystemMessages.MSG87));
            return;
        }

        await _next(context);
    }

    private static bool IsAllowed(HttpRequest request) =>
        Allowed.Any(a => string.Equals(a.Method, request.Method, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(a.Path, request.Path.Value?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
}
