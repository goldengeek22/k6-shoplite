using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ShopLite.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? throw new InvalidOperationException("Authenticated user has no 'sub' claim.");

        return long.Parse(sub);
    }
}
