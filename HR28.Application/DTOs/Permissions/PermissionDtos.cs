namespace HR28.Application.DTOs.Permissions;

public class PermissionInfoDto
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    /// <summary>View, Add, Edit, Delete or Link.</summary>
    public string Action { get; set; } = string.Empty;
}

public class RolePermissionsDto
{
    public Guid RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    /// <summary>True for the Administrator role, which always has every right.</summary>
    public bool HasAllPermissions { get; set; }

    public List<string> Permissions { get; set; } = new();

    public string Description { get; set; } = string.Empty;

    /// <summary>Built-in roles can't be renamed or deleted (the app relies on their names).</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>"Full" or "AddEncounter": what opens when someone with this role opens a voter.</summary>
    public string VoterProfileView { get; set; } = "Full";

    /// <summary>"Dashboard" or "QuickEntry": where someone with this role lands after signing in.</summary>
    public string StartPage { get; set; } = "Dashboard";

    /// <summary>People who have this role (a role in use can't be deleted).</summary>
    public int UserCount { get; set; }
}

/// <summary>Create or change a role (name and description are ignored for built-in roles).</summary>
public class SaveRoleDto
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string VoterProfileView { get; set; } = "Full";

    /// <summary>"Dashboard" or "QuickEntry". Missing keeps the current value on update.</summary>
    public string? StartPage { get; set; }
}

public class PermissionMatrixDto
{
    public List<PermissionInfoDto> Permissions { get; set; } = new();

    public List<RolePermissionsDto> Roles { get; set; } = new();
}

public class SetPermissionsDto
{
    public List<string> Permissions { get; set; } = new();
}
