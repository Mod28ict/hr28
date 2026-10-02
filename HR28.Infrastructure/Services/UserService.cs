using HR28.Application.Common;
using HR28.Application.DTOs.Users;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Domain.Enums;
using HR28.Infrastructure.Data;
using HR28.Infrastructure.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly HR28DbContext _dbContext;
    private readonly IAuthorizationCodeHasher _codeHasher;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccessScopeService _accessScopeService;

    public UserService(
        HR28DbContext dbContext,
        IAuthorizationCodeHasher codeHasher,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IAccessScopeService accessScopeService)
    {
        _dbContext = dbContext;
        _codeHasher = codeHasher;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _accessScopeService = accessScopeService;
    }

    private Guid? GetCurrentUserId()
    {
        var value = _httpContextAccessor.HttpContext?.User
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>Generates a code whose hash is not already in use.</summary>
    private async Task<(string Code, string Hash)> NewUniqueCodeAsync()
    {
        while (true)
        {
            var code = AuthorizationCodeGenerator.Generate();
            var hash = _codeHasher.Hash(code);

            if (!await _dbContext.Users.AnyAsync(u => u.AuthorizationCodeHash == hash))
                return (code, hash);
        }
    }

    public async Task<UserDto> CreateUserAsync(
        CreateUserDto request)
    {
        var (authorizationCode, codeHash) = await NewUniqueCodeAsync();

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
            AuthorizationCodeHash = codeHash,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId() ?? Guid.Empty
        };

        _dbContext.Users.Add(user);

        _dbContext.AuthorizationCodeHistories.Add(new AuthorizationCodeHistory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AuthorizationCodeHash = codeHash,
            CreatedAt = DateTime.UtcNow,
            Reason = "Issued when the account was created"
        });

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(), "Create", "User", user.Id.ToString());

        return new UserDto
        {
            Id = user.Id,
            NationalId = user.NationalId,
            FullName = user.FullName,
            Address = user.Address,
            MobileNumber = user.MobileNumber,
            Email = user.Email,
            Designation = user.Designation,

            // Returned this once so the administrator can hand it over.
            AuthorizationCode = authorizationCode,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<string?> ResetAuthorizationCodeAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return null;

        var (code, hash) = await NewUniqueCodeAsync();

        var now = DateTime.UtcNow;

        var previous = await _dbContext.AuthorizationCodeHistories
            .Where(h => h.UserId == userId && h.RevokedAt == null)
            .ToListAsync();

        foreach (var entry in previous)
            entry.RevokedAt = now;

        user.AuthorizationCodeHash = hash;
        user.AuthorizationCode = null;

        _dbContext.AuthorizationCodeHistories.Add(new AuthorizationCodeHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AuthorizationCodeHash = hash,
            CreatedAt = now,
            Reason = "Reset by an administrator"
        });

        // Any code already sent by SMS for the old authorization code stops working too.
        var openOtps = await _dbContext.OtpRequests
            .Where(o => o.UserId == userId && !o.IsUsed)
            .ToListAsync();

        foreach (var otp in openOtps)
            otp.IsUsed = true;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(), "Reset authorization code", "User", userId.ToString());

        return code;
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
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,

                RoleName =
                    user.UserRoles
                        .Select(x => x.Role.Name)
                        .FirstOrDefault(),

                ConstituencyName =
                    user.UserScopes
                        .Select(x => x.Constituency.Name)
                        .FirstOrDefault(),

                IslandName =
                    user.UserScopes
                        .Select(x => x.Island.Name)
                        .FirstOrDefault()
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
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,

                RoleName =
                    user.UserRoles
                        .Select(x => x.Role.Name)
                        .FirstOrDefault(),

                ConstituencyName =
                    user.UserScopes
                        .Select(x => x.Constituency.Name)
                        .FirstOrDefault(),

                IslandName =
                    user.UserScopes
                        .Select(x => x.Island.Name)
                        .FirstOrDefault()
            })
            .FirstOrDefaultAsync();
    }
    /// <summary>
    /// Sets the user's role, replacing any previous role (the screen offers a single
    /// choice). Previously this only added roles, so a demoted user kept old rights.
    /// </summary>
    public async Task AssignRoleAsync(
    Guid userId,
    Guid roleId)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == roleId)
            ?? throw new BusinessRuleException("Please choose a valid role.");

        if (!await _dbContext.Users.AnyAsync(u => u.Id == userId))
            throw new KeyNotFoundException("User not found.");

        var current = await _dbContext.UserRoles
            .Include(x => x.Role)
            .Where(x => x.UserId == userId)
            .ToListAsync();

        if (current.Count == 1 && current[0].RoleId == roleId)
            return;

        // Never remove the last active Administrator, or nobody could manage users.
        var losingSuperAdmin =
            current.Any(x => x.Role.Name == AccessScopeService.SuperAdministratorRole) &&
            role.Name != AccessScopeService.SuperAdministratorRole;

        if (losingSuperAdmin)
        {
            var otherSuperAdmins = await _dbContext.UserRoles.CountAsync(x =>
                x.UserId != userId &&
                x.User.IsActive &&
                x.Role.Name == AccessScopeService.SuperAdministratorRole);

            if (otherSuperAdmins == 0)
            {
                throw new BusinessRuleException(
                    "This is the only Administrator. Make another user an Administrator first.");
            }
        }

        var oldNames = string.Join(", ", current.Select(x => x.Role.Name).OrderBy(n => n));

        _dbContext.UserRoles.RemoveRange(current);
        _dbContext.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = roleId
        });

        await _dbContext.SaveChangesAsync();

        _accessScopeService.Invalidate(userId);

        await _auditService.LogAsync(
            GetCurrentUserId(),
            $"Role changed from \"{(oldNames.Length == 0 ? "none" : oldNames)}\" to \"{role.Name}\"",
            "User",
            userId.ToString());
    }

    public async Task AssignScopeAsync(
        Guid userId,
        Guid? constituencyId,
        Guid? islandId)
    {
        if (!await _dbContext.Users.AnyAsync(u => u.Id == userId))
            throw new KeyNotFoundException("User not found.");

        var existingScope = await _dbContext.UserScopes
            .Include(x => x.Constituency)
            .Include(x => x.Island)
            .FirstOrDefaultAsync(x => x.UserId == userId);

        string Describe(string? constituency, string? island) =>
            constituency == null
                ? "none"
                : island == null ? $"{constituency} (all islands)" : $"{island}, {constituency}";

        var oldScope = Describe(existingScope?.Constituency?.Name, existingScope?.Island?.Name);

        if (existingScope != null)
        {
            existingScope.ConstituencyId = constituencyId;
            existingScope.IslandId = islandId;
        }
        else
        {
            _dbContext.UserScopes.Add(new UserScope
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConstituencyId = constituencyId,
                IslandId = islandId
            });
        }

        await _dbContext.SaveChangesAsync();

        _accessScopeService.Invalidate(userId);

        var newScope = Describe(
            constituencyId == null ? null : await _dbContext.Constituencies.Where(c => c.Id == constituencyId).Select(c => c.Name).FirstOrDefaultAsync(),
            islandId == null ? null : await _dbContext.Islands.Where(i => i.Id == islandId).Select(i => i.Name).FirstOrDefaultAsync());

        await _auditService.LogAsync(
            GetCurrentUserId(),
            $"Scope changed from \"{oldScope}\" to \"{newScope}\"",
            "User",
            userId.ToString());
    }

}