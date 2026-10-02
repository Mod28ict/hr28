namespace HR28.Application.DTOs.Users;

/// <summary>One area a user can see: a whole constituency, or one island in it.</summary>
public class UserScopeDto
{
    public Guid Id { get; set; }

    public Guid? ConstituencyId { get; set; }

    public string ConstituencyName { get; set; } = string.Empty;

    public Guid? IslandId { get; set; }

    public string? IslandName { get; set; }

    /// <summary>Readable form, e.g. "Malé Central (all islands)" or "Thinadhoo, Thinadhoo Dhekunu".</summary>
    public string Label =>
        IslandName == null
            ? $"{ConstituencyName} (all islands)"
            : $"{IslandName}, {ConstituencyName}";
}

public class SetRolesDto
{
    public List<Guid> RoleIds { get; set; } = new();
}

/// <summary>Role names in order of authority, used to pick the role shown first.</summary>
public static class RoleOrder
{
    public static readonly string[] ByAuthority =
    {
        "Super Administrator",
        "National Administrator",
        "Constituency Administrator",
        "Island Administrator",
        "Collector",
        "Reporter"
    };

    public static int Rank(string role)
    {
        var index = Array.IndexOf(ByAuthority, role);
        return index < 0 ? ByAuthority.Length : index;
    }

    public static List<string> Sort(IEnumerable<string> roles) =>
        roles.Distinct().OrderBy(Rank).ThenBy(r => r).ToList();
}
