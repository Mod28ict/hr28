using HR28.Application.Common;
using HR28.Application.DTOs.Permissions;
using HR28.Application.DTOs.Users;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly HR28DbContext _dbContext;
    private readonly IAccessScopeService _accessScopeService;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionService(
        HR28DbContext dbContext,
        IAccessScopeService accessScopeService,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _accessScopeService = accessScopeService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    private Guid? GetCurrentUserId()
    {
        var value = _httpContextAccessor.HttpContext?.User
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static List<string> Validate(IReadOnlyCollection<string> permissions)
    {
        var wanted = permissions.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();

        var unknown = wanted.FirstOrDefault(p => !PermissionCatalog.IsKnown(p));

        if (unknown != null)
            throw new BusinessRuleException("One of the chosen rights is not recognised. Please reload the page.");

        return wanted;
    }

    private static string DescribeChange(IEnumerable<string> added, IEnumerable<string> removed)
    {
        var parts = new List<string>();

        if (added.Any())
            parts.Add("granted " + string.Join(", ", added.Select(PermissionCatalog.NameOf)));

        if (removed.Any())
            parts.Add("removed " + string.Join(", ", removed.Select(PermissionCatalog.NameOf)));

        return string.Join("; ", parts);
    }

    public async Task<PermissionMatrixDto> GetMatrixAsync()
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.VoterProfileView,
                UserCount = r.UserRoles.Count
            })
            .ToListAsync();

        var grants = await _dbContext.RolePermissions
            .AsNoTracking()
            .ToListAsync();

        return new PermissionMatrixDto
        {
            Permissions = PermissionCatalog.All
                .Select(p => new PermissionInfoDto
                {
                    Key = p.Key,
                    Name = p.Name,
                    Description = p.Description,
                    Group = p.Group,
                    Action = p.Action
                })
                .ToList(),

            Roles = roles
                .OrderBy(r => RoleOrder.Rank(r.Name))
                .ThenBy(r => r.Name)
                .Select(r => new RolePermissionsDto
                {
                    RoleId = r.Id,
                    RoleName = r.Name,
                    HasAllPermissions = r.Name == AccessScopeService.SuperAdministratorRole,
                    Description = r.Description,
                    IsBuiltIn = IsBuiltIn(r.Name),
                    VoterProfileView = r.Name == AccessScopeService.SuperAdministratorRole
                        ? VoterProfileViews.Full
                        : r.VoterProfileView,
                    UserCount = r.UserCount,
                    Permissions = grants
                        .Where(g => g.RoleId == r.Id)
                        .Select(g => g.Permission)
                        .ToList()
                })
                .ToList()
        };
    }

    public async Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissions)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == roleId)
            ?? throw new KeyNotFoundException("Role not found.");

        if (role.Name == AccessScopeService.SuperAdministratorRole)
            throw new BusinessRuleException("The Administrator role always has every right.");

        var wanted = Validate(permissions);

        var current = await _dbContext.RolePermissions
            .Where(p => p.RoleId == roleId)
            .ToListAsync();

        var removed = current.Where(c => !wanted.Contains(c.Permission)).ToList();
        var added = wanted.Where(w => current.All(c => c.Permission != w)).ToList();

        if (removed.Count == 0 && added.Count == 0)
            return;

        _dbContext.RolePermissions.RemoveRange(removed);
        _dbContext.RolePermissions.AddRange(
            added.Select(p => new RolePermission { RoleId = roleId, Permission = p }));

        await _dbContext.SaveChangesAsync();

        // Affects everyone with this role.
        _accessScopeService.InvalidateAll();

        await _auditService.LogAsync(
            GetCurrentUserId(),
            $"Rights for role \"{role.Name}\": " + DescribeChange(added, removed.Select(r => r.Permission)),
            "Role",
            role.Name);
    }

    /// <summary>Roles the app relies on by name: they can't be renamed or deleted.</summary>
    private static bool IsBuiltIn(string roleName) => RoleOrder.ByAuthority.Contains(roleName);

    private async Task<(string Name, string Description)> ValidateRoleAsync(SaveRoleDto request, Guid? existingId)
    {
        var name = string.Join(' ', (request.Name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var description = (request.Description ?? string.Empty).Trim();

        if (name.Length < 2 || name.Length > 50)
            throw new BusinessRuleException("Role names must be between 2 and 50 characters.");

        // "|" separates roles in the web session.
        if (name.Contains('|'))
            throw new BusinessRuleException("Role names can't contain the | character.");

        if (description.Length > 200)
            throw new BusinessRuleException("The description can be at most 200 characters.");

        if (IsBuiltIn(name) || name.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException($"\"{name}\" is a built-in role name. Please choose another name.");

        // SQL Server compares names case-insensitively.
        if (await _dbContext.Roles.AnyAsync(r => r.Name == name && r.Id != existingId))
            throw new BusinessRuleException($"There is already a role called \"{name}\".");

        return (name, description);
    }

    private static string RequireProfileView(string? view) =>
        VoterProfileViews.All.FirstOrDefault(v => v == view)
        ?? throw new BusinessRuleException("Please choose what opens when someone opens a voter.");

    public async Task<Guid> CreateRoleAsync(SaveRoleDto request)
    {
        var (name, description) = await ValidateRoleAsync(request, null);
        var view = RequireProfileView(request.VoterProfileView);

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            VoterProfileView = view
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetCurrentUserId(),
            $"Create role (opens voters on: {VoterProfileViews.Label(view)})",
            "Role",
            name);

        return role.Id;
    }

    public async Task UpdateRoleAsync(Guid roleId, SaveRoleDto request)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == roleId)
            ?? throw new KeyNotFoundException("Role not found.");

        var view = RequireProfileView(request.VoterProfileView);
        var changes = new List<string>();

        if (role.Name == AccessScopeService.SuperAdministratorRole)
        {
            if (view != VoterProfileViews.Full)
                throw new BusinessRuleException("The Administrator always sees the full voter profile.");

            return;
        }

        if (!IsBuiltIn(role.Name))
        {
            var (name, description) = await ValidateRoleAsync(request, roleId);

            if (role.Name != name) changes.Add($"name \"{role.Name}\" → \"{name}\"");
            if (role.Description != description) changes.Add("description");

            role.Name = name;
            role.Description = description;
        }

        if (role.VoterProfileView != view)
        {
            changes.Add($"opens voters on \"{VoterProfileViews.Label(role.VoterProfileView)}\" → \"{VoterProfileViews.Label(view)}\"");
            role.VoterProfileView = view;
        }

        if (changes.Count == 0)
            return;

        await _dbContext.SaveChangesAsync();

        // Affects everyone with this role.
        _accessScopeService.InvalidateAll();

        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Update role: " + string.Join(", ", changes),
            "Role",
            role.Name);
    }

    public async Task<string> DeleteRoleAsync(Guid roleId)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == roleId)
            ?? throw new KeyNotFoundException("Role not found.");

        if (IsBuiltIn(role.Name))
            throw new BusinessRuleException($"{role.Name} is a built-in role and can't be deleted.");

        var holders = await _dbContext.UserRoles.CountAsync(ur => ur.RoleId == roleId);

        if (holders > 0)
            throw new BusinessRuleException(
                $"{holders} {(holders == 1 ? "person has" : "people have")} the role \"{role.Name}\". " +
                "Remove it from them first (Users → Roles).");

        // Its rights (RolePermissions) are removed with it (cascade).
        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync();

        _accessScopeService.InvalidateAll();

        await _auditService.LogAsync(GetCurrentUserId(), "Delete role", "Role", role.Name);

        return role.Name;
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid userId)
    {
        if (!await _dbContext.Users.AnyAsync(u => u.Id == userId))
            throw new KeyNotFoundException("User not found.");

        return await _dbContext.UserPermissions
            .Where(p => p.UserId == userId)
            .Select(p => p.Permission)
            .ToListAsync();
    }

    public async Task SetUserPermissionsAsync(Guid userId, IReadOnlyCollection<string> permissions)
    {
        if (!await _dbContext.Users.AnyAsync(u => u.Id == userId))
            throw new KeyNotFoundException("User not found.");

        var wanted = Validate(permissions);

        var current = await _dbContext.UserPermissions
            .Where(p => p.UserId == userId)
            .ToListAsync();

        var removed = current.Where(c => !wanted.Contains(c.Permission)).ToList();
        var added = wanted.Where(w => current.All(c => c.Permission != w)).ToList();

        if (removed.Count == 0 && added.Count == 0)
            return;

        _dbContext.UserPermissions.RemoveRange(removed);
        _dbContext.UserPermissions.AddRange(
            added.Select(p => new UserPermission { UserId = userId, Permission = p }));

        await _dbContext.SaveChangesAsync();

        _accessScopeService.Invalidate(userId);

        await _auditService.LogAsync(
            GetCurrentUserId(),
            "Extra rights: " + DescribeChange(added, removed.Select(r => r.Permission)),
            "User",
            userId.ToString());
    }
}
