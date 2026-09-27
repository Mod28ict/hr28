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
}