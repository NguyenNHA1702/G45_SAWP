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

    public static string? GetUsername(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Name)?.Value;
    }

    public static bool HasRole(this ClaimsPrincipal principal, UserRoles role)
    {
        return principal.IsInRole(role.ToString());
    }
}
