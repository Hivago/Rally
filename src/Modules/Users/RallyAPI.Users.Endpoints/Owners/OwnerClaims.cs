using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

internal static class OwnerClaims
{
    /// <summary>
    /// Owner id for an "Owner"-policy caller: the owner_id claim on an outlet token
    /// (owner switched into an outlet), otherwise sub on the owner's own token.
    /// </summary>
    public static Guid GetOwnerId(this ClaimsPrincipal user)
    {
        var raw = user.HasClaim("user_type", "owner")
            ? user.FindFirstValue("sub")
            : user.FindFirstValue("owner_id");

        return Guid.Parse(raw!);
    }
}
