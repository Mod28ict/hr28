namespace HR28.Application.DTOs.Permissions;

public class PermissionInfoDto
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;
}

public class RolePermissionsDto
{
    public Guid RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    /// <summary>True for the Administrator role, which always has every right.</summary>
    public bool HasAllPermissions { get; set; }

    public List<string> Permissions { get; set; } = new();
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
