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

    public UserService(
        HR28DbContext dbContext,
        IAuthorizationCodeHasher codeHasher,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _codeHasher = codeHasher;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
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
        var existingScope = await _dbContext.UserScopes
            .FirstOrDefaultAsync(x => x.UserId == userId);

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
    }

}