using HR28.Application.DTOs.Permissions;

namespace HR28.Application.Interfaces;

/// <summary>Grants rights to roles and to individual users. Administrator only.</summary>
public interface IPermissionService
{
    Task<PermissionMatrixDto> GetMatrixAsync();

    Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissions);

    Task<List<string>> GetUserPermissionsAsync(Guid userId);

    Task SetUserPermissionsAsync(Guid userId, IReadOnlyCollection<string> permissions);
}
