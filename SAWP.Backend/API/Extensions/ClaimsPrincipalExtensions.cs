using System.Security.Claims;
using Core.Enums;

namespace API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                      ?? principal.FindFirst("id")?.Value;
        return int.TryParse(idClaim, out int id) ? id : null;
    }

    public static int GetRequiredUserId(this ClaimsPrincipal principal)
    {
        return principal.GetUserId() ?? throw new UnauthorizedAccessException("User ID is missing from claims.");
    }

    public static string? GetUsername(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Name)?.Value;
    }

    public static bool HasRole(this ClaimsPrincipal principal, UserRoles role)
    {
        return principal.IsInRole(role.ToString());
    }

    public static IReadOnlyList<UserRoles> GetRoles(this ClaimsPrincipal principal)
    {
        var roles = new List<UserRoles>();
        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
        foreach (var r in roleClaims)
        {
            if (Enum.TryParse<UserRoles>(r, true, out var roleEnum))
            {
                roles.Add(roleEnum);
            }
        }
        return roles;
    }

}
