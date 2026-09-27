using HR28.Application.DTOs.Users;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Domain.Enums;
using HR28.Infrastructure.Data;
using HR28.Infrastructure.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly HR28DbContext _dbContext;

    public UserService(HR28DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserDto> CreateUserAsync(
        CreateUserDto request)
    {
        string authorizationCode;

        do
        {
            authorizationCode =
                AuthorizationCodeGenerator.Generate();
        }
        while (await _dbContext.Users.AnyAsync(u =>
            u.AuthorizationCode == authorizationCode));

        var user = new User
        {
            Id = Guid.NewGuid(),
            NationalId = request.NationalId,
            FullName = request.FullName,
            Address = request.Address,
            MobileNumber = request.MobileNumber,
            Email = request.Email,
            Designation = request.Designation,
            Remarks = request.Remarks,
            AuthorizationCode = authorizationCode,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        _dbContext.Users.Add(user);

        await _dbContext.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id,
            NationalId = user.NationalId,
            FullName = user.FullName,
            Address = user.Address,
            MobileNumber = user.MobileNumber,
            Email = user.Email,
            Designation = user.Designation,
            AuthorizationCode = user.AuthorizationCode,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        return await _dbContext.Users
            .Select(user => new UserDto
            {
                Id = user.Id,
                NationalId = user.NationalId,
                FullName = user.FullName,
                Address = user.Address,
                MobileNumber = user.MobileNumber,
                Email = user.Email,
                Designation = user.Designation,
                AuthorizationCode = user.AuthorizationCode,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt
            })
            .ToListAsync();
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        return await _dbContext.Users
            .Where(user => user.Id == id)
            .Select(user => new UserDto
            {
                Id = user.Id,
                NationalId = user.NationalId,
                FullName = user.FullName,
                Address = user.Address,
                MobileNumber = user.MobileNumber,
                Email = user.Email,
                Designation = user.Designation,
                AuthorizationCode = user.AuthorizationCode,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt
            })
            .FirstOrDefaultAsync();
    }
    public async Task AssignRoleAsync(
    Guid userId,
    Guid roleId)
    {
        var existing = await _dbContext.UserRoles
            .AnyAsync(x =>
                x.UserId == userId &&
                x.RoleId == roleId);

        if (existing)
            return;

        _dbContext.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = roleId
        });

        await _dbContext.SaveChangesAsync();
    }
    public async Task AssignScopeAsync(
    Guid userId,
    Guid? constituencyId,
    Guid? islandId)
    {
        _dbContext.UserScopes.Add(new UserScope
        {
            UserId = userId,
            ScopeLevel = constituencyId.HasValue
                ? ScopeLevel.Constituency
                : ScopeLevel.Island,

            ConstituencyId = constituencyId,
            IslandId = islandId
        });

        await _dbContext.SaveChangesAsync();
    }
}