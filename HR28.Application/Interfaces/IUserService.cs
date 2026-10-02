using HR28.Application.DTOs.Users;

namespace HR28.Application.Interfaces;

public interface IUserService
{
    Task<UserDto> CreateUserAsync(
        CreateUserDto request);

    Task<List<UserDto>> GetUsersAsync();

    Task<UserDto?> GetUserByIdAsync(
        Guid id);
    Task AssignRoleAsync(
    Guid userId,
    Guid roleId);
    Task AssignScopeAsync(
    Guid userId,
    Guid? constituencyId,
    Guid? islandId);

    /// <summary>Replaces the user's roles with exactly this set (at least one).</summary>
    Task SetRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds);

    /// <summary>Adds an area (whole constituency, or one island) to the user.</summary>
    Task<UserScopeDto> AddScopeAsync(Guid userId, Guid? constituencyId, Guid? islandId);

    Task RemoveScopeAsync(Guid userId, Guid scopeId);

    /// <summary>
    /// Issues a new authorization code, revokes the old one, and returns the
    /// new code. This is the only time the code is available in plain text.
    /// Returns null if the user does not exist.
    /// </summary>
    Task<string?> ResetAuthorizationCodeAsync(Guid userId);
}