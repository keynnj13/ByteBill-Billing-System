using System.Security.Claims;

namespace ByteBill_BS.Extensions;

/// <summary>
/// Convenience methods for extracting tenant/user context from the cookie claims.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal principal)
        => long.TryParse(principal.FindFirstValue("UserId"), out var id) ? id : 0;

    public static long GetShopId(this ClaimsPrincipal principal)
        => long.TryParse(principal.FindFirstValue("ShopId"), out var id) ? id : 0;

    public static string GetRole(this ClaimsPrincipal principal)
        => principal.FindFirstValue("Role") ?? string.Empty;

    public static string GetFullName(this ClaimsPrincipal principal)
        => principal.FindFirstValue("FullName") ?? string.Empty;

    public static bool IsInRoles(this ClaimsPrincipal principal, params string[] roles)
        => roles.Contains(principal.GetRole(), StringComparer.OrdinalIgnoreCase);
}
