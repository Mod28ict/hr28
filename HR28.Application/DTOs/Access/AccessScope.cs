namespace HR28.Application.DTOs.Access;

/// <summary>
/// What a user is allowed to see. Administrators see everything;
/// everyone else is limited to their assigned constituencies and islands.
/// </summary>
public class AccessScope
{
    public Guid UserId { get; init; }

    /// <summary>False if the account is missing or deactivated: no access at all.</summary>
    public bool IsActive { get; init; }

    /// <summary>The user's current roles, read from the database (not the login token).</summary>
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    /// <summary>Sees every record (Super or National Administrator).</summary>
    public bool IsAdministrator { get; init; }

    /// <summary>The client's Administrator (stored as "Super Administrator"): manages users, roles and scopes.</summary>
    public bool IsSuperAdministrator { get; init; }

    /// <summary>
    /// Rights from the user's roles plus rights granted to them directly.
    /// The Administrator has every right.
    /// </summary>
    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();

    public bool HasPermission(string permission) =>
        IsActive && (IsSuperAdministrator || Permissions.Contains(permission));

    /// <summary>Constituencies the user can see in full (scope with no island).</summary>
    public IReadOnlyList<Guid> ConstituencyIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Individual islands the user can see.</summary>
    public IReadOnlyList<Guid> IslandIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Constituencies the user touches at all, including through an island scope.</summary>
    public IReadOnlyList<Guid> VisibleConstituencyIds { get; init; } = Array.Empty<Guid>();

    public bool HasAnyScope =>
        IsAdministrator || ConstituencyIds.Count > 0 || IslandIds.Count > 0;

    /// <summary>True if a record in this constituency/island is inside the scope.</summary>
    public bool Allows(Guid constituencyId, Guid? islandId)
    {
        if (IsAdministrator)
            return true;

        if (ConstituencyIds.Contains(constituencyId))
            return true;

        return islandId.HasValue && IslandIds.Contains(islandId.Value);
    }
}
