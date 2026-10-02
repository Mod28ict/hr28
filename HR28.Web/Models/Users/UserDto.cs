namespace HR28.Web.Models.Users;

public class UserDto
{
    public Guid Id { get; set; }

    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;

    public string AuthorizationCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public string? RoleName { get; set; }

    public string? ConstituencyName { get; set; }

    public string? IslandName { get; set; }

    /// <summary>All roles, most senior first.</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>All areas the user can see.</summary>
    public List<UserScopeDto> Scopes { get; set; } = new();
}

public class UserScopeDto
{
    public Guid Id { get; set; }

    public Guid? ConstituencyId { get; set; }

    public string ConstituencyName { get; set; } = string.Empty;

    public Guid? IslandId { get; set; }

    public string? IslandName { get; set; }

    public string Label { get; set; } = string.Empty;
}

/// <summary>Roles page: tick every role the user should have.</summary>
public class UserRolesViewModel
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public List<Guid> SelectedRoleIds { get; set; } = new();

    public List<RoleDto> Roles { get; set; } = new();

    /// <summary>Rights granted to this person on top of their roles.</summary>
    public List<string> SelectedPermissions { get; set; } = new();

    public List<HR28.Web.Models.PermissionInfo> AvailablePermissions { get; set; } = new();
}

/// <summary>Areas page: current areas plus a form to add one.</summary>
public class UserAreasViewModel
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public List<UserScopeDto> Scopes { get; set; } = new();

    public List<HR28.Web.Models.LookupDto> Constituencies { get; set; } = new();

    public bool IsAdministrator { get; set; }
}