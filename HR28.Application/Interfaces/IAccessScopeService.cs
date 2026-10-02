using HR28.Application.DTOs.Access;

namespace HR28.Application.Interfaces;

/// <summary>
/// Resolves a user's current roles, scopes and active status on the server.
/// Results are cached briefly; call <see cref="Invalidate"/> after changing a
/// user's roles, scopes or status so the change applies immediately.
/// </summary>
public interface IAccessScopeService
{
    Task<AccessScope> GetAsync(Guid userId);

    void Invalidate(Guid userId);
}
