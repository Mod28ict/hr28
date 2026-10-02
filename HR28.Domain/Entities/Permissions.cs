namespace HR28.Domain.Entities;

/// <summary>A right granted to everyone who has the role.</summary>
public class RolePermission
{
    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    /// <summary>A key from PermissionCatalog, e.g. "Influencers.Edit".</summary>
    public string Permission { get; set; } = string.Empty;
}

/// <summary>An extra right granted to one user on top of their roles.</summary>
public class UserPermission
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>A key from PermissionCatalog, e.g. "Influencers.Delete".</summary>
    public string Permission { get; set; } = string.Empty;
}
