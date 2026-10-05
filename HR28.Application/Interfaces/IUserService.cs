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

    /// <summary>
    /// Changes a user's details and whether the account is active. Changed fields are
    /// audited; a new mobile number is announced by SMS to the old number.
    /// </summary>
    Task UpdateUserAsync(Guid userId, UpdateUserDto request);

    /// <summary>
    /// Activates or deactivates an account (Users list status pop-up). Same rules as
    /// Edit: not your own account, not the last active Administrator. Audited.
    /// </summary>
    Task SetActiveAsync(Guid userId, bool isActive);

    /// <summary>
    /// Permanently deletes an account that has not recorded encounters or pledges
    /// (those records must keep who made them; deactivate such accounts instead).
    /// Returns the deleted user's name.
    /// </summary>
    Task<string> DeleteUserAsync(Guid userId);
}