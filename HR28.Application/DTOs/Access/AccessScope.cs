namespace HR28.Application.DTOs.Access;

/// <summary>
/// What a user is allowed to see. Administrators see everything;
/// everyone else is limited to their assigned constituencies and islands.
/// </summary>
public class AccessScope
{
    public Guid UserId { get; init; }

    public bool IsAdministrator { get; init; }

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
