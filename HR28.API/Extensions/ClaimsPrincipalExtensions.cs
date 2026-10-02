using System.Security.Claims;

namespace HR28.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in user's id, or null if the token has no valid id claim.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
    }
}
