using HR28.Application.DTOs.Permissions;

namespace HR28.Application.Interfaces;

/// <summary>Grants rights to roles and to individual users. Administrator only.</summary>
public interface IPermissionService
{
    Task<PermissionMatrixDto> GetMatrixAsync();

    Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissions);

    Task<List<string>> GetUserPermissionsAsync(Guid userId);

    Task SetUserPermissionsAsync(Guid userId, IReadOnlyCollection<string> permissions);

    /// <summary>A new custom role with no rights yet. Returns its id.</summary>
    Task<Guid> CreateRoleAsync(SaveRoleDto request);

    /// <summary>Name and description (custom roles only) and the voter-profile view.</summary>
    Task UpdateRoleAsync(Guid roleId, SaveRoleDto request);

    /// <summary>Custom roles that nobody has. Returns the deleted role's name.</summary>
    Task<string> DeleteRoleAsync(Guid roleId);
}
